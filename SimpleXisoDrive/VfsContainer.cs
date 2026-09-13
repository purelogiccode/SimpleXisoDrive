using SimpleXisoDrive.Vfs;

namespace SimpleXisoDrive;

/// <summary>
/// Provides a read-only virtual file system view over an Xbox image, resolving paths
/// to directory entries and serving file data to the Dokan layer.
/// </summary>
/// <remarks>
/// Xbox ISO/XISO images are exposed through <see cref="XisoVfsVolume"/>; ZArchive
/// (<c>.zar</c>) files are exposed through <see cref="ZarVfsVolume"/> (directory tree)
/// or as an embedded XISO image. The volume implementation is chosen automatically by
/// <see cref="VfsVolumeFactory"/>.
/// </remarks>
public class VfsContainer : IDisposable
{
    private readonly IVfsVolume _volume;

    /// <summary>
    /// Gets the total size of the image in bytes.
    /// </summary>
    public ulong VolumeSize => _volume.VolumeSize;

    /// <summary>
    /// Gets the volume creation time.
    /// </summary>
    public DateTime VolumeCreationTime => _volume.VolumeCreationTime;

    /// <summary>
    /// Gets the volume label reported to Windows.
    /// </summary>
    public string VolumeLabel => _volume.VolumeLabel;

    /// <summary>
    /// Gets the file system name reported to Windows.
    /// </summary>
    public string FileSystemName => _volume.FileSystemName;

    /// <summary>
    /// Initializes a new instance of the <see cref="VfsContainer"/> class for the specified image file.
    /// </summary>
    /// <param name="imagePath">The path to the Xbox ISO/XISO or ZArchive (<c>.zar</c>) file to open.</param>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid Xbox image or ZArchive.</exception>
    public VfsContainer(string imagePath)
    {
        _volume = VfsVolumeFactory.Open(imagePath);
    }

    /// <summary>
    /// Gets the file entry for the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual path to look up.</param>
    /// <returns>The matching entry, or <see langword="null"/> if no entry exists at the path.</returns>
    public IVfsEntry? GetEntry(string path) => _volume.GetEntry(path);

    /// <summary>
    /// Enumerates the child entries of the directory at the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual directory path to list.</param>
    /// <returns>The entries contained in the directory; empty if the path is not a valid directory.</returns>
    public IEnumerable<IVfsEntry> GetFolderList(string path) => _volume.GetFolderList(path);

    /// <summary>
    /// Reads file data for the specified entry into the buffer.
    /// </summary>
    /// <param name="entry">The file entry to read from.</param>
    /// <param name="buffer">The buffer that receives the data.</param>
    /// <param name="offset">The byte offset within the file at which to start reading.</param>
    /// <returns>The number of bytes read, or zero if the read fails.</returns>
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset) => _volume.ReadFile(entry, buffer, offset);

    /// <summary>
    /// Closes the underlying image or archive stream.
    /// </summary>
    public void Dispose()
    {
        _volume.Dispose();
    }
}
