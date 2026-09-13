using Serilog;
using ZArchiveSharp;

namespace SimpleXisoDrive.Vfs;

/// <summary>
/// Opens the correct <see cref="IVfsVolume"/> implementation for an image file.
/// Xbox ISO/XISO images are opened directly; ZArchive (<c>.zar</c>) files expose either
/// an embedded XISO image or their archived directory tree.
/// </summary>
internal static class VfsVolumeFactory
{
    private const string ZarExtension = ".zar";

    /// <summary>
    /// Opens <paramref name="imagePath"/> as an Xbox ISO or ZArchive volume.
    /// </summary>
    /// <param name="imagePath">The path to the image to open.</param>
    /// <returns>The volume that exposes the image's file system.</returns>
    /// <exception cref="InvalidImageException">Thrown when the file is neither a valid Xbox ISO nor a valid ZArchive.</exception>
    public static IVfsVolume Open(string imagePath)
    {
        if (HasExtension(imagePath, ZarExtension))
        {
            return OpenZar(imagePath);
        }

        try
        {
            return new XisoVfsVolume(imagePath);
        }
        catch (InvalidImageException)
        {
            // A ZArchive renamed to .iso (or another extension) should still mount.
            var reader = ZarVfsVolume.TryOpenArchive(imagePath);
            if (reader is null)
            {
                throw;
            }

            Log.Information("'{ImagePath}' is not an Xbox ISO; opening it as a ZArchive.", imagePath);
            return OpenZar(reader, imagePath);
        }
    }

    private static IVfsVolume OpenZar(string archivePath)
    {
        var reader = ZarVfsVolume.TryOpenArchive(archivePath) ?? throw new InvalidImageException(
            $"'{archivePath}' is not a valid ZArchive (.zar) file.");

        return OpenZar(reader, archivePath);
    }

    /// <summary>
    /// Opens an archive that is already being read. Ownership of <paramref name="reader"/>
    /// transfers to the returned volume (or to the probe on the way there).
    /// </summary>
    private static IVfsVolume OpenZar(ZArchiveReader reader, string archivePath)
    {
        if (TryMountEmbeddedXiso(reader, archivePath, out var embeddedIso))
        {
            return embeddedIso;
        }

        return new ZarVfsVolume(reader, archivePath);
    }

    /// <summary>
    /// Detects the single-embedded-XISO layout (used for lossless Redump rebuilds):
    /// exactly one file at the archive root whose data validates as an XDVDFS image.
    /// When the probe fails the reader stays open for the directory-tree view.
    /// </summary>
    private static bool TryMountEmbeddedXiso(ZArchiveReader reader, string archivePath, out IVfsVolume volume)
    {
        volume = null!;

        if (reader.GetDirEntryCount(0) != 1 ||
            !reader.GetDirEntry(0, 0, out var only) ||
            !only.IsFile)
        {
            return false;
        }

        var node = reader.LookUp(only.Name);
        if (node == ZArchiveReader.InvalidNode)
        {
            return false;
        }

        var size = reader.GetFileSize(node);
        if (size == 0 || size > long.MaxValue)
        {
            return false;
        }

        // leaveOpen keeps the archive usable when the file is not an XISO; ownership
        // is handed to the stream only after the image has been validated.
        var stream = new ZarNodeStream(reader, node, (long)size, leaveOpen: true);
        try
        {
            volume = new XisoVfsVolume(stream, archivePath);
            stream.TakeOwnership();
            return true;
        }
        catch (InvalidImageException ex)
        {
            Log.Debug(ex, "'{ArchivePath}' does not embed an Xbox ISO image", archivePath);
            return false;
        }
    }

    private static bool HasExtension(string path, string extension)
    {
        return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }
}
