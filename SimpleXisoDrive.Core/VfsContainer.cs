using Serilog;
using SimpleXisoDrive.Core.Interfaces;
using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Core;

/// <summary>
/// Provides a read-only virtual file system view over an Xbox image, resolving paths
/// to directory entries and serving file data to the Dokan layer.
/// </summary>
/// <remarks>
/// Xbox ISO/XISO images and Xbox ISO CHDs are exposed through
/// <see cref="XisoVfsVolume"/>; ZArchive (<c>.zar</c>) files are exposed through
/// <see cref="ZarVfsVolume"/> (directory tree) or as an embedded XISO image. The volume
/// implementation is chosen automatically by
/// <see cref="VfsVolumeFactory"/>. With <c>exposeImageIso</c>, an
/// <see cref="ImageIsoVfsVolume"/> decorator adds the raw image as <c>image.iso</c>.
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
    /// <param name="imagePath">The path to the Xbox ISO/XISO, Xbox ISO CHD or ZArchive (<c>.zar</c>) file to open.</param>
    /// <param name="exposeImageIso">
    /// When <see langword="true"/>, the mount also exposes the raw Xbox image as a virtual
    /// <c>image.iso</c> file at the volume root, for emulators that only accept a disc image
    /// (such as xemu). ZArchive directory trees are synthesized into a virtual XISO image in memory.
    /// </param>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid Xbox image or ZArchive.</exception>
    public VfsContainer(string imagePath, bool exposeImageIso = false)
    {
        try
        {
            _volume = VfsVolumeFactory.Open(imagePath, exposeImageIso);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open image '{ImagePath}' as a virtual volume", imagePath);
            throw;
        }
    }

    /// <summary>
    /// Gets the file entry for the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual path to look up.</param>
    /// <returns>The matching entry, or <see langword="null"/> if no entry exists at the path.</returns>
    public IVfsEntry? GetEntry(string path)
    {
        try
        {
            return _volume.GetEntry(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetEntry failed for '{Path}'", path);
            throw;
        }
    }

    /// <summary>
    /// Enumerates the child entries of the directory at the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual directory path to list.</param>
    /// <returns>The entries contained in the directory; empty if the path is not a valid directory.</returns>
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        try
        {
            return _volume.GetFolderList(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetFolderList failed for '{Path}'", path);
            throw;
        }
    }

    /// <summary>
    /// Reads file data for the specified entry into the buffer.
    /// </summary>
    /// <param name="entry">The file entry to read from.</param>
    /// <param name="buffer">The buffer that receives the data.</param>
    /// <param name="offset">The byte offset within the file at which to start reading.</param>
    /// <returns>The number of bytes read, or zero if the read fails.</returns>
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        try
        {
            return _volume.ReadFile(entry, buffer, offset);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ReadFile failed for '{FileName}' at offset {Offset}", entry.FileName, offset);
            throw;
        }
    }

    /// <summary>
    /// Closes the underlying image or archive stream.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _volume.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to dispose the virtual volume");
        }
    }
}