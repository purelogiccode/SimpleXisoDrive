using FuseSharp;
using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive;

/// <summary>
/// Adapts an <see cref="IVfsVolume"/> (backslash-separated VFS paths) to the POSIX-path
/// <see cref="IFuseVolume"/> contract consumed by FuseSharp.
/// </summary>
internal sealed class FuseVolumeAdapter : IFuseVolume
{
    private readonly IVfsVolume _volume;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuseVolumeAdapter"/> class.
    /// </summary>
    /// <param name="volume">The volume to adapt.</param>
    public FuseVolumeAdapter(IVfsVolume volume)
    {
        _volume = volume;
    }

    /// <inheritdoc />
    public string VolumeLabel => _volume.VolumeLabel;

    /// <inheritdoc />
    public DateTime VolumeCreationTime => _volume.VolumeCreationTime;

    /// <inheritdoc />
    public ulong VolumeSize => _volume.VolumeSize;

    /// <inheritdoc />
    public IFuseEntry? GetEntry(string path)
    {
        var entry = _volume.GetEntry(ToVfsPath(path));
        return entry is null ? null : new EntryAdapter(entry);
    }

    /// <inheritdoc />
    public IEnumerable<IFuseEntry> GetFolderList(string path)
    {
        foreach (var entry in _volume.GetFolderList(ToVfsPath(path)))
        {
            yield return new EntryAdapter(entry);
        }
    }

    /// <inheritdoc />
    public int ReadFile(IFuseEntry entry, Span<byte> buffer, long offset)
    {
        if (entry is not EntryAdapter adapter)
        {
            throw new ArgumentException("The entry was not created by this volume.", nameof(entry));
        }

        return _volume.ReadFile(adapter.Inner, buffer, offset);
    }

    /// <summary>
    /// Converts a POSIX path to the backslash-separated path used by the VFS.
    /// </summary>
    /// <param name="fusePath">The POSIX path (<c>/</c> for the root).</param>
    /// <returns>The VFS path.</returns>
    internal static string ToVfsPath(string fusePath)
    {
        if (string.IsNullOrEmpty(fusePath) || string.Equals(fusePath, "/", StringComparison.Ordinal))
        {
            return "\\";
        }

        return fusePath.Replace('/', '\\');
    }

    private sealed class EntryAdapter : IFuseEntry
    {
        public EntryAdapter(IVfsEntry entry)
        {
            Inner = entry;
        }

        public IVfsEntry Inner { get; }

        public string FileName => Inner.FileName;

        public bool IsDirectory => Inner.IsDirectory;

        public long Size => Inner.Size;
    }
}
