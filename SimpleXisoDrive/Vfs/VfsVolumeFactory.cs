using Serilog;
using XISOSharp;
using ZArchiveSharp;

namespace SimpleXisoDrive.Vfs;

/// <summary>
/// Opens the correct <see cref="IVfsVolume"/> implementation for an image file.
/// Xbox ISO/XISO images are opened directly; ZArchive (<c>.zar</c>) files expose either
/// an embedded XISO image or their archived directory tree. When requested, the raw
/// image is also exposed as a virtual <c>image.iso</c> file at the volume root.
/// </summary>
internal static class VfsVolumeFactory
{
    private const string ZarExtension = ".zar";

    /// <summary>
    /// Opens <paramref name="imagePath"/> as an Xbox ISO or ZArchive volume.
    /// </summary>
    /// <param name="imagePath">The path to the image to open.</param>
    /// <param name="exposeImageIso">
    /// When <see langword="true"/>, the volume also exposes the raw Xbox image as a
    /// virtual <c>image.iso</c> file for emulators that only accept disc images.
    /// </param>
    /// <returns>The volume that exposes the image's file system.</returns>
    /// <exception cref="InvalidImageException">Thrown when the file is neither a valid Xbox ISO nor a valid ZArchive.</exception>
    public static IVfsVolume Open(string imagePath, bool exposeImageIso = false)
    {
        if (HasExtension(imagePath, ZarExtension))
        {
            return OpenZar(ZarVfsVolume.OpenArchiveOrThrow(imagePath), imagePath, exposeImageIso);
        }

        XisoVfsVolume xisoVolume;
        try
        {
            xisoVolume = new XisoVfsVolume(imagePath);
        }
        catch (InvalidImageException)
        {
            // A ZArchive renamed to .iso (or another extension) should still mount.
            var reader = ZarVfsVolume.TryOpenArchive(imagePath, out _);
            if (reader is null)
            {
                throw;
            }

            Log.Information("'{ImagePath}' is not an Xbox ISO; opening it as a ZArchive.", imagePath);
            return OpenZar(reader, imagePath, exposeImageIso);
        }

        if (!exposeImageIso)
        {
            return xisoVolume;
        }

        IRawImageSource? source = null;
        try
        {
            source = OpenPathRawImageSource(imagePath);
            return new ImageIsoVfsVolume(xisoVolume, source);
        }
        catch
        {
            source?.Dispose();
            xisoVolume.Dispose();
            throw;
        }
    }

    private static IVfsVolume OpenZar(string archivePath, bool exposeImageIso)
    {
        return OpenZar(ZarVfsVolume.OpenArchiveOrThrow(archivePath), archivePath, exposeImageIso);
    }

    /// <summary>
    /// Opens an archive that is already being read. Ownership of <paramref name="reader"/>
    /// transfers to the returned volume (or to the probe on the way there).
    /// </summary>
    private static IVfsVolume OpenZar(ZArchiveReader reader, string archivePath, bool exposeImageIso)
    {
        if (TryMountEmbeddedXiso(reader, archivePath, out var embeddedVolume, out var embeddedNode))
        {
            if (!exposeImageIso)
            {
                return embeddedVolume;
            }

            IRawImageSource? embeddedSource = null;
            try
            {
                embeddedSource = new StreamRawImageSource(reader.OpenRead(embeddedNode));
                return new ImageIsoVfsVolume(embeddedVolume, embeddedSource);
            }
            catch
            {
                embeddedSource?.Dispose();
                embeddedVolume.Dispose();
                throw;
            }
        }

        var treeVolume = new ZarVfsVolume(reader, archivePath);
        if (!exposeImageIso)
        {
            return treeVolume;
        }

        IRawImageSource? virtualSource = null;
        try
        {
            virtualSource = VirtualXisoImageSource.Create(reader, archivePath);
            return new ImageIsoVfsVolume(treeVolume, virtualSource);
        }
        catch
        {
            virtualSource?.Dispose();
            treeVolume.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens the raw image bytes for a path-based image: CISO inputs are decompressed
    /// on demand through the block device, plain ISO/XISO files are read directly.
    /// </summary>
    private static IRawImageSource OpenPathRawImageSource(string imagePath) =>
        new StreamRawImageSource(XisoReader.OpenImageStream(imagePath, FileShare.ReadWrite));

    /// <summary>
    /// Detects the single-embedded-XISO layout (used for lossless Redump rebuilds):
    /// exactly one file at the archive root whose data validates as an XDVDFS image.
    /// When the probe fails the reader stays open for the directory-tree view; when the
    /// probe itself errors, the reader is disposed with the failure.
    /// </summary>
    private static bool TryMountEmbeddedXiso(ZArchiveReader reader, string archivePath, out IVfsVolume volume,
        out uint embeddedNode)
    {
        volume = null!;
        embeddedNode = ZArchiveReader.InvalidNode;

        uint node;
        try
        {
            if (reader.GetDirEntryCount(ZArchiveReader.RootNode) != 1 ||
                !reader.TryGetDirEntry(ZArchiveReader.RootNode, 0, out node, out var only) ||
                !only.IsFile)
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            // The tree cannot be read either; the reader is dead weight now.
            reader.Dispose();
            Log.Debug(ex, "'{ArchivePath}' root entry could not be read", archivePath);
            throw;
        }

        try
        {
            // The library's OpenRead stream deliberately does not own the reader,
            // so the wrapper closes both; a failed XISO probe disposes only the
            // stream, leaving the reader open for the directory-tree fallback.
            var stream = reader.OpenRead(node);
            var isoVolume = new XisoVfsVolume(stream, archivePath);
            volume = new ReaderOwningVfsVolume(isoVolume, reader);
            embeddedNode = node;
            return true;
        }
        catch (InvalidImageException ex)
        {
            Log.Debug(ex, "'{ArchivePath}' does not embed an Xbox ISO image", archivePath);
            return false;
        }
        catch (Exception ex)
        {
            // The probe failed before ownership transferred: never leak the reader.
            reader.Dispose();
            Log.Debug(ex, "'{ArchivePath}' could not be opened for the embedded-ISO probe", archivePath);
            throw;
        }
    }

    private static bool HasExtension(string path, string extension)
    {
        return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }
}
