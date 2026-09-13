namespace SimpleXisoDrive.Vfs;

/// <summary>
/// A read-only volume that resolves paths to entries and serves file data to the Dokan layer.
/// Implemented by <see cref="XisoVfsVolume"/> (Xbox ISO / XISO images) and
/// <see cref="ZarVfsVolume"/> (ZArchive directory trees).
/// </summary>
public interface IVfsVolume : IDisposable
{
    /// <summary>
    /// Gets the total size of the volume in bytes.
    /// </summary>
    ulong VolumeSize { get; }

    /// <summary>
    /// Gets the volume creation time.
    /// </summary>
    DateTime VolumeCreationTime { get; }

    /// <summary>
    /// Gets the volume label reported to Windows.
    /// </summary>
    string VolumeLabel { get; }

    /// <summary>
    /// Gets the file system name reported to Windows.
    /// </summary>
    string FileSystemName { get; }

    /// <summary>
    /// Gets the entry for the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual path to look up.</param>
    /// <returns>The matching entry, or <see langword="null"/> if no entry exists at the path.</returns>
    IVfsEntry? GetEntry(string path);

    /// <summary>
    /// Enumerates the child entries of the directory at the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual directory path to list.</param>
    /// <returns>The entries contained in the directory; empty if the path is not a valid directory.</returns>
    IEnumerable<IVfsEntry> GetFolderList(string path);

    /// <summary>
    /// Reads file data for the specified entry into the buffer.
    /// </summary>
    /// <param name="entry">The file entry to read from.</param>
    /// <param name="buffer">The buffer that receives the data.</param>
    /// <param name="offset">The byte offset within the file at which to start reading.</param>
    /// <returns>The number of bytes read, or zero if the read fails.</returns>
    int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset);
}
