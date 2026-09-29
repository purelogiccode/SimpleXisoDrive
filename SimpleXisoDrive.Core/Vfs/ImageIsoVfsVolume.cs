using Serilog;
using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// Decorates an <see cref="IVfsVolume"/> so that the mount also exposes the raw
/// Xbox image as a virtual <c>image.iso</c> file at the volume root. Emulators
/// that only accept a raw disc image (such as xemu) can open
/// <c>&lt;mount&gt;\image.iso</c> while the normal file tree stays available for
/// browsing. When the wrapped volume already contains a real file with that
/// name, the real entry wins and no synthetic file is added.
/// </summary>
internal sealed class ImageIsoVfsVolume : IVfsVolume
{
    /// <summary>The name of the virtual raw image file.</summary>
    private const string ImageIsoName = "image.iso";

    private const string ImageIsoPath = "\\" + ImageIsoName;

    private readonly IVfsVolume _inner;
    private readonly IRawImageSource _source;
    private readonly ImageIsoEntry _entry;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageIsoVfsVolume"/> class.
    /// </summary>
    /// <param name="inner">The volume that serves the mounted file tree.</param>
    /// <param name="source">The raw image backing the virtual <c>image.iso</c> file.</param>
    public ImageIsoVfsVolume(IVfsVolume inner, IRawImageSource source)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(source);

            _inner = inner;
            _source = source;
            _entry = new ImageIsoEntry(source.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create the image.iso volume decorator");
            throw;
        }
    }

    /// <inheritdoc />
    public ulong VolumeSize => _inner.VolumeSize + (ulong)_source.Length;

    /// <inheritdoc />
    public DateTime VolumeCreationTime => _inner.VolumeCreationTime;

    /// <inheritdoc />
    public string VolumeLabel => _inner.VolumeLabel;

    /// <inheritdoc />
    public string FileSystemName => _inner.FileSystemName;

    /// <inheritdoc />
    public IVfsEntry? GetEntry(string path)
    {
        try
        {
            var existing = _inner.GetEntry(path);
            return existing ?? (IsImageIsoPath(path) ? _entry : null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImageIsoVfsVolume.GetEntry failed for '{Path}'", path);
            throw;
        }
    }

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        try
        {
            var children = _inner.GetFolderList(path).ToList();

            if (IsRoot(path) &&
                !children.Exists(static child =>
                    string.Equals(child.FileName, ImageIsoName, StringComparison.OrdinalIgnoreCase)))
            {
                children.Add(_entry);
            }

            return children;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImageIsoVfsVolume.GetFolderList failed for '{Path}'", path);
            throw;
        }
    }

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        try
        {
            return ReferenceEquals(entry, _entry)
                ? _source.Read(buffer, offset)
                : _inner.ReadFile(entry, buffer, offset);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImageIsoVfsVolume.ReadFile failed for '{FileName}' at offset {Offset}",
                entry.FileName, offset);
            throw;
        }
    }

    private static bool IsImageIsoPath(string path)
    {
        return string.Equals(NormalizePath(path), ImageIsoPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoot(string path)
    {
        return string.Equals(NormalizePath(path), "\\", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        var normalizedPath = path.Replace('/', '\\').TrimEnd('\\');
        return string.IsNullOrEmpty(normalizedPath) ? "\\" : normalizedPath;
    }

    /// <summary>
    /// Disposes the raw image source and the wrapped volume.
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
            _source.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImageIsoVfsVolume.Dispose failed for the raw image source");
        }

        try
        {
            _inner.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImageIsoVfsVolume.Dispose failed for the wrapped volume");
        }
    }

    /// <summary>
    /// A synthetic read-only file entry backed by the raw image source.
    /// </summary>
    private sealed class ImageIsoEntry(long size) : IVfsEntry
    {
        /// <inheritdoc />
        public string FileName => ImageIsoName;

        /// <inheritdoc />
        public bool IsDirectory => false;

        /// <inheritdoc />
        public long Size { get; } = size;

        /// <inheritdoc />
        public FileAttributes GetWindowsAttributes()
        {
            return FileAttributes.ReadOnly | FileAttributes.Normal;
        }
    }
}