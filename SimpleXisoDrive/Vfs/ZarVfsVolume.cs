using System.Collections.Concurrent;
using Serilog;
using ZArchiveSharp;

namespace SimpleXisoDrive.Vfs;

/// <summary>
/// Provides a read-only virtual file system view over the directory tree stored in a
/// ZArchive (<c>.zar</c>) file, resolving paths to archive nodes and serving decompressed
/// file data to the Dokan layer.
/// </summary>
public sealed class ZarVfsVolume : IVfsVolume
{
    private readonly ZArchiveReader _reader;
    private readonly ConcurrentDictionary<string, ZarEntry> _entryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<IVfsEntry>> _childrenCache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ulong VolumeSize { get; }

    /// <inheritdoc />
    public DateTime VolumeCreationTime { get; }

    /// <inheritdoc />
    public string VolumeLabel => "XBOX_ZAR";

    /// <inheritdoc />
    public string FileSystemName => "ZARCHIVE";

    /// <summary>
    /// Initializes a new instance of the <see cref="ZarVfsVolume"/> class for the specified archive.
    /// </summary>
    /// <param name="archivePath">The path to the ZArchive (<c>.zar</c>) file to open.</param>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid ZArchive.</exception>
    public ZarVfsVolume(string archivePath)
        : this(
            TryOpenArchive(archivePath, out var failure) ?? throw new InvalidImageException(
                $"'{archivePath}' is not a valid ZArchive (.zar) file ({failure})."),
            archivePath)
    {
    }

    /// <summary>
    /// Opens a ZArchive with <see cref="FileShare.ReadWrite"/> so that scanners and indexing
    /// tools can keep the file open while it is mounted. Returns <see langword="null"/> when
    /// the file cannot be opened or is not a valid archive;
    /// <paramref name="failure"/> carries the specific reason.
    /// </summary>
    internal static ZArchiveReader? TryOpenArchive(string archivePath, out ZArchiveOpenFailure failure)
    {
        return ZArchiveReader.TryOpen(
            archivePath,
            new ZArchiveReaderOptions { FileShare = FileShare.ReadWrite },
            out failure);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZarVfsVolume"/> class over an open archive.
    /// The volume takes ownership of <paramref name="reader"/>.
    /// </summary>
    internal ZarVfsVolume(ZArchiveReader reader, string archivePath)
    {
        _reader = reader;

        try
        {
            VolumeCreationTime = TryGetCreationTime(archivePath);
            VolumeSize = reader.TotalUncompressedSize;

            CacheEntry("\\",
                new ZarEntry(ZArchiveReader.RootNode, string.Empty, isDirectory: true, size: 0));
        }
        catch (Exception ex)
        {
            _reader.Dispose();
            Log.Error(ex, "Failed to read ZArchive '{ArchivePath}'", archivePath);
            throw new InvalidImageException($"Failed to read ZArchive: {ex.Message}", ex);
        }
    }

    private static DateTime TryGetCreationTime(string archivePath)
    {
        try
        {
            return File.Exists(archivePath) ? File.GetCreationTime(archivePath) : DateTime.Now;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private void CacheEntry(string path, ZarEntry entry)
    {
        _entryCache[path] = entry;
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
            Log.Error(ex, "ZarVfsVolume.GetEntry failed for '{Path}'", path);
            return null;
        }
    }

    private ZarEntry? GetEntryInternal(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (_entryCache.TryGetValue(normalizedPath, out var cachedEntry))
        {
            return cachedEntry;
        }

        var node = _reader.LookUp(normalizedPath);
        if (node == ZArchiveReader.InvalidNode)
        {
            return null;
        }

        return CreateEntry(normalizedPath, node);
    }

    private ZarEntry CreateEntry(string normalizedPath, uint node)
    {
        var isDirectory = _reader.IsDirectory(node);
        var size = isDirectory ? 0 : (long)Math.Min(_reader.GetFileSize(node), long.MaxValue);
        var fileName = _reader.TryGetNodeName(node, out var name) ? name : string.Empty;

        var entry = new ZarEntry(node, fileName, isDirectory, size);
        CacheEntry(normalizedPath, entry);
        return entry;
    }

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (_childrenCache.TryGetValue(normalizedPath, out var cachedChildren))
        {
            foreach (var entry in cachedChildren) yield return entry;

            yield break;
        }

        if (GetEntry(normalizedPath) is not ZarEntry { IsDirectory: true } directory)
        {
            yield break;
        }

        var children = new List<IVfsEntry>();
        var atRoot = string.Equals(normalizedPath, "\\", StringComparison.Ordinal);
        var childCount = _reader.GetDirEntryCount(directory.Node);

        for (uint i = 0; i < childCount; i++)
        {
            // TryGetDirEntry resolves the child handle in one step (the library
            // clamps the count and bounds-checks the index), so no path rebuild
            // or second lookup is needed per child.
            if (!_reader.TryGetDirEntry(directory.Node, i, out var childNode, out var child) ||
                string.IsNullOrEmpty(child.Name))
            {
                continue;
            }

            var childPath = atRoot ? "\\" + child.Name : normalizedPath + "\\" + child.Name;
            var size = child.IsDirectory ? 0 : (long)Math.Min(child.Size, long.MaxValue);
            var childEntry = new ZarEntry(childNode, child.Name, child.IsDirectory, size);
            CacheEntry(childPath, childEntry);
            children.Add(childEntry);
            yield return childEntry;
        }

        _childrenCache[normalizedPath] = children;
    }

    private static string NormalizePath(string path)
    {
        var normalizedPath = path.Replace('/', '\\').TrimEnd('\\');
        return string.IsNullOrEmpty(normalizedPath) ? "\\" : normalizedPath;
    }

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        try
        {
            if (entry is not ZarEntry { IsDirectory: false } zarEntry || offset < 0)
            {
                return 0;
            }

            return (int)_reader.ReadFromFile(zarEntry.Node, (ulong)offset, buffer);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ZarVfsVolume.ReadFile failed for {FileName}", entry.FileName);
            return 0;
        }
    }

    /// <summary>
    /// Closes the underlying archive.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _reader.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ZarVfsVolume.Dispose failed");
        }
    }

    /// <summary>
    /// A file or directory entry backed by a ZArchive node handle.
    /// </summary>
    private sealed class ZarEntry(uint node, string fileName, bool isDirectory, long size) : IVfsEntry
    {
        /// <summary>
        /// Gets the ZArchive node handle used to read the entry's children or data.
        /// </summary>
        public uint Node { get; } = node;

        /// <inheritdoc />
        public string FileName { get; } = fileName;

        /// <inheritdoc />
        public bool IsDirectory { get; } = isDirectory;

        /// <inheritdoc />
        public long Size { get; } = size;

        /// <inheritdoc />
        public FileAttributes GetWindowsAttributes()
        {
            // ZArchive stores no attributes: expose everything as read-only and
            // mark regular files as normal so Explorer does not treat them as folders.
            return IsDirectory
                ? FileAttributes.ReadOnly | FileAttributes.Directory
                : FileAttributes.ReadOnly | FileAttributes.Normal;
        }
    }
}
