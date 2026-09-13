using System.Collections.Concurrent;
using Serilog;
using XISOSharp;
using XISOSharp.Models;

namespace SimpleXisoDrive.Vfs;

/// <summary>
/// Provides a read-only virtual file system view over an Xbox ISO/XISO image,
/// resolving paths to directory entries and serving file data to the Dokan layer.
/// </summary>
/// <remarks>
/// All XDVDFS parsing is delegated to the XISOSharp library. Path-based images
/// use a keep-open <see cref="XisoExplorer"/> (one shared image handle for the
/// lifetime of the mount); images embedded in another container, such as a
/// ZArchive, are read through the <see cref="XisoReader"/> stream APIs.
/// </remarks>
public sealed class XisoVfsVolume : IVfsVolume
{
    /// <summary>The XDVDFS sector size in bytes; always 2048.</summary>
    private const int SectorSize = 2048;

    /// <summary>Raw XDVDFS attribute byte for a directory entry.</summary>
    private const byte DirectoryAttribute = 0x10;

    private readonly XisoExplorer? _explorer;
    private readonly Stream? _stream;
    private readonly string _displayName;
    private readonly VolumeInfo _volume;
    private readonly Lock _streamLock = new();
    private readonly ConcurrentDictionary<string, XisoEntry> _entryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<IVfsEntry>> _childrenCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <inheritdoc />
    public ulong VolumeSize => (ulong)_volume.FileLength;

    /// <inheritdoc />
    public DateTime VolumeCreationTime => _volume.CreationTime?.LocalDateTime ?? DateTime.MinValue;

    /// <inheritdoc />
    public string VolumeLabel => "XBOX_ISO";

    /// <inheritdoc />
    public string FileSystemName => "XDVDFS";

    /// <summary>
    /// Initializes a new instance of the <see cref="XisoVfsVolume"/> class for the specified ISO file.
    /// </summary>
    /// <param name="isoPath">The path to the Xbox ISO file to open.</param>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid Xbox ISO image.</exception>
    public XisoVfsVolume(string isoPath)
    {
        _displayName = isoPath;

        try
        {
            // KeepOpen gives the volume one shared image handle for its lifetime;
            // ReadWrite sharing lets scanners/indexers keep the file open while mounted.
            _explorer = new XisoExplorer(isoPath, new XisoExplorerOptions
            {
                KeepOpen = true,
                Share = FileShare.ReadWrite,
            });
        }
        catch (Exception ex) when (ex is XisoFormatException or InvalidDataException or EndOfStreamException)
        {
            Log.Debug(ex, "Invalid Xbox ISO image '{ImagePath}'", isoPath);
            throw new InvalidImageException($"'{isoPath}' is not a valid Xbox ISO/XISO image.", ex);
        }

        _volume = _explorer.Volume;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XisoVfsVolume"/> class over an already-open stream.
    /// Used to mount an XISO image embedded inside a ZArchive. The volume takes ownership of the stream.
    /// </summary>
    /// <param name="stream">A seekable, read-only stream positioned at the start of the image.</param>
    /// <param name="displayName">The display name used in log and error messages.</param>
    /// <exception cref="InvalidImageException">Thrown when the stream is not a valid Xbox ISO image.</exception>
    internal XisoVfsVolume(Stream stream, string displayName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        _displayName = displayName;

        try
        {
            _volume = XisoReader.GetVolumeInfo(stream, displayName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            DisposeStream(stream);
            Log.Debug(ex, "Invalid Xbox ISO image '{ImagePath}'", displayName);
            throw new InvalidImageException($"Failed to read Xbox ISO: {ex.Message}", ex);
        }

        if (!_volume.IsValid)
        {
            DisposeStream(stream);
            throw new InvalidImageException("XDVDFS magic string not found.");
        }
    }

    private static void DisposeStream(Stream stream)
    {
        try
        {
            stream.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to dispose image stream");
        }
    }

    /// <inheritdoc />
    public IVfsEntry? GetEntry(string path)
    {
        try
        {
            return GetEntryInternal(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetEntry failed for '{Path}'", path);
            return null;
        }
    }

    private XisoEntry? GetEntryInternal(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (_entryCache.TryGetValue(normalizedPath, out var cachedEntry))
        {
            return cachedEntry;
        }

        if (string.Equals(normalizedPath, "\\", StringComparison.Ordinal))
        {
            return CacheEntry(normalizedPath, new XisoEntry(string.Empty, isDirectory: true, size: 0,
                _volume.RootDirSector, DirectoryAttribute, node: null));
        }

        var libraryPath = XisoExplorer.Normalize(normalizedPath);

        XisoEntry? entry;
        if (_explorer is not null)
        {
            var node = _explorer.GetNode(libraryPath);
            entry = node is null
                ? null
                : new XisoEntry(node.Name, node.IsDirectory, node.Size, node.StartSector, node.Attributes, node);
        }
        else
        {
            EntryInfo? info;
            lock (_streamLock)
            {
                info = XisoReader.GetEntryInfo(_stream!, _displayName, libraryPath);
            }

            entry = info is null
                ? null
                : new XisoEntry(info.Name, info.IsDirectory, info.FileSize, info.StartSector, info.Attributes,
                    node: null);
        }

        return entry is null ? null : CacheEntry(normalizedPath, entry);
    }

    private XisoEntry CacheEntry(string normalizedPath, XisoEntry entry)
    {
        _entryCache[normalizedPath] = entry;
        return entry;
    }

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (_childrenCache.TryGetValue(normalizedPath, out var cachedChildren))
        {
            return cachedChildren;
        }

        try
        {
            if (GetEntryInternal(normalizedPath) is not { IsDirectory: true })
            {
                return [];
            }

            var libraryPath = XisoExplorer.Normalize(normalizedPath);
            var children = new List<IVfsEntry>();

            if (_explorer is not null)
            {
                foreach (var node in _explorer.ListChildren(libraryPath))
                {
                    var child = new XisoEntry(node.Name, node.IsDirectory, node.Size, node.StartSector, node.Attributes,
                        node);
                    CacheEntry(CombinePath(normalizedPath, node.Name), child);
                    children.Add(child);
                }
            }
            else
            {
                IReadOnlyList<EntryInfo> entries;
                lock (_streamLock)
                {
                    entries = XisoReader.ListDirectory(_stream!, _displayName, libraryPath);
                }

                foreach (var info in entries)
                {
                    var child = new XisoEntry(info.Name, info.IsDirectory, info.FileSize, info.StartSector,
                        info.Attributes, node: null);
                    CacheEntry(CombinePath(normalizedPath, info.Name), child);
                    children.Add(child);
                }
            }

            _childrenCache[normalizedPath] = children;
            Log.Debug("[GetFolderList] Cached {Count} children for '{NormalizedPath}'", children.Count,
                normalizedPath);
            return children;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetFolderList failed for '{Path}'", path);
            return [];
        }
    }

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        try
        {
            if (entry is not XisoEntry { IsDirectory: false } xisoEntry || offset < 0 || offset >= xisoEntry.Size ||
                buffer.IsEmpty)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(buffer.Length, xisoEntry.Size - offset);

            if (_explorer is not null && xisoEntry.Node is not null)
            {
                using var fileStream = _explorer.OpenReadStream(xisoEntry.Node);
                fileStream.Seek(offset, SeekOrigin.Begin);
                return ReadFully(fileStream, buffer[..bytesToRead]);
            }

            lock (_streamLock)
            {
                var position = _volume.DiscLseek + ((long)xisoEntry.StartSector * SectorSize) + offset;
                if (position >= _stream!.Length)
                {
                    return 0;
                }

                _stream.Seek(position, SeekOrigin.Begin);
                return ReadFully(_stream, buffer[..bytesToRead]);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "XisoVfsVolume.ReadFile failed for {FileName}", entry.FileName);
            return 0;
        }
    }

    private static int ReadFully(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read <= 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static string NormalizePath(string path)
    {
        var normalizedPath = path.Replace('/', '\\').TrimEnd('\\');
        return string.IsNullOrEmpty(normalizedPath) ? "\\" : normalizedPath;
    }

    private static string CombinePath(string directory, string name) =>
        string.Equals(directory, "\\", StringComparison.Ordinal) ? "\\" + name : directory + "\\" + name;

    /// <summary>
    /// Closes the underlying image handle.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _explorer?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "XisoVfsVolume.Dispose failed");
        }

        if (_stream is not null)
        {
            DisposeStream(_stream);
        }
    }

    /// <summary>
    /// A file or directory entry backed by an XISOSharp
    /// <see cref="ExplorerNode"/> (path-based images) or <see cref="EntryInfo"/> (stream-based images).
    /// </summary>
    private sealed class XisoEntry(
        string fileName,
        bool isDirectory,
        long size,
        uint startSector,
        byte rawAttributes,
        ExplorerNode? node) : IVfsEntry
    {
        /// <summary>
        /// Gets the explorer node used to open the entry's data stream, or
        /// <see langword="null"/> for stream-based volumes and the synthetic root.
        /// </summary>
        public ExplorerNode? Node { get; } = node;

        /// <summary>
        /// Gets the partition-relative sector where the entry's data begins.
        /// </summary>
        public uint StartSector { get; } = startSector;

        /// <summary>
        /// Gets the raw XDVDFS attribute byte of the entry.
        /// </summary>
        private byte RawAttributes { get; } = rawAttributes;

        /// <inheritdoc />
        public string FileName { get; } = fileName;

        /// <inheritdoc />
        public bool IsDirectory { get; } = isDirectory;

        /// <inheritdoc />
        public long Size { get; } = size;

        /// <inheritdoc />
        public FileAttributes GetWindowsAttributes() => XisoAttributes.ToWindowsFileAttributes(RawAttributes);
    }
}
