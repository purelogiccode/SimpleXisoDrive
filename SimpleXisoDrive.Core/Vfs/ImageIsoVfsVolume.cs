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
    private readonly bool _rawImageIsAdditionalContent;
    private List<IVfsEntry>? _rootListing;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageIsoVfsVolume"/> class for
    /// mounts where the raw image is the same image the wrapped volume is mounted
    /// from (plain ISO/XISO/CISO, CHD, embedded XISO).
    /// </summary>
    /// <param name="inner">The volume that serves the mounted file tree.</param>
    /// <param name="source">The raw image backing the virtual <c>image.iso</c> file.</param>
    public ImageIsoVfsVolume(IVfsVolume inner, IRawImageSource source)
        : this(inner, source, rawImageIsAdditionalContent: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageIsoVfsVolume"/> class.
    /// </summary>
    /// <param name="inner">The volume that serves the mounted file tree.</param>
    /// <param name="source">The raw image backing the virtual <c>image.iso</c> file.</param>
    /// <param name="rawImageIsAdditionalContent">
    /// When <see langword="true"/>, the raw image is extra content on top of the
    /// wrapped volume (a ZArchive directory tree synthesized into an XISO) and its
    /// length counts towards the reported volume size. When <see langword="false"/>,
    /// the raw image is the same image the wrapped volume already accounts for.
    /// </param>
    public ImageIsoVfsVolume(IVfsVolume inner, IRawImageSource source, bool rawImageIsAdditionalContent)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(source);

            _inner = inner;
            _source = source;
            _rawImageIsAdditionalContent = rawImageIsAdditionalContent;
            _entry = new ImageIsoEntry(source.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create the image.iso volume decorator");
            throw;
        }
    }

    /// <inheritdoc />
    public ulong VolumeSize => _rawImageIsAdditionalContent
        ? _inner.VolumeSize + (ulong)_source.Length
        : _inner.VolumeSize;

    /// <inheritdoc />
    public DateTime VolumeCreationTime => _inner.VolumeCreationTime;

    /// <inheritdoc />
    public string VolumeLabel => _inner.VolumeLabel;

    /// <inheritdoc />
    public string FileSystemName => _inner.FileSystemName;

    /// <inheritdoc />
    public IVfsEntry? GetEntry(string path)
    {
        var existing = _inner.GetEntry(path);
        return existing ?? (IsImageIsoPath(path) ? _entry : null);
    }

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        if (IsRoot(path))
        {
            // Directory listings are cached instances, matching the other volumes;
            // callers must treat them as read-only.
            _rootListing ??= BuildRootListing();
            return _rootListing;
        }

        return _inner.GetFolderList(path);
    }

    private List<IVfsEntry> BuildRootListing()
    {
        var children = _inner.GetFolderList("\\").ToList();

        if (!children.Exists(static child =>
                string.Equals(child.FileName, ImageIsoName, StringComparison.OrdinalIgnoreCase)))
        {
            children.Add(_entry);
        }

        return children;
    }

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        return ReferenceEquals(entry, _entry)
            ? _source.Read(buffer, offset)
            : _inner.ReadFile(entry, buffer, offset);
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