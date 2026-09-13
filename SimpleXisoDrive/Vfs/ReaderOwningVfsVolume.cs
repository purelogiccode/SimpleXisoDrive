namespace SimpleXisoDrive.Vfs;

/// <summary>
/// Decorates an <see cref="IVfsVolume"/> so that disposing it also disposes the
/// archive reader backing its stream. <see cref="ZArchiveSharp.ZArchiveReader.OpenRead"/>
/// streams only own themselves, while an embedded-XISO mount needs the reader kept
/// alive for the lifetime of the volume and closed together with it.
/// </summary>
/// <param name="inner">The volume that serves the embedded image.</param>
/// <param name="owner">The archive reader to dispose with <paramref name="inner"/>.</param>
internal sealed class ReaderOwningVfsVolume(IVfsVolume inner, IDisposable owner) : IVfsVolume
{
    private readonly IVfsVolume _inner = inner;
    private readonly IDisposable _owner = owner;

    /// <inheritdoc />
    public ulong VolumeSize => _inner.VolumeSize;

    /// <inheritdoc />
    public DateTime VolumeCreationTime => _inner.VolumeCreationTime;

    /// <inheritdoc />
    public string VolumeLabel => _inner.VolumeLabel;

    /// <inheritdoc />
    public string FileSystemName => _inner.FileSystemName;

    /// <inheritdoc />
    public IVfsEntry? GetEntry(string path) => _inner.GetEntry(path);

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path) => _inner.GetFolderList(path);

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset) => _inner.ReadFile(entry, buffer, offset);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _inner.Dispose();
        }
        finally
        {
            _owner.Dispose();
        }
    }
}
