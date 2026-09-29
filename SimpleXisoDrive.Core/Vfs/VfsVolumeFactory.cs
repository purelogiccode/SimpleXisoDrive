using CHDSharp;
using Serilog;
using SimpleXisoDrive.Core.Interfaces;
using XISOSharp;
using ZArchiveSharp;

namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// Opens the correct <see cref="IVfsVolume"/> implementation for an image file.
/// Xbox ISO/XISO images are opened directly; Xbox ISO CHD (<c>.chd</c>) files are
/// decompressed on demand through CHDSharp; ZArchive (<c>.zar</c>) files expose either
/// an embedded XISO image or their archived directory tree. When requested, the raw
/// image is also exposed as a virtual <c>image.iso</c> file at the volume root.
/// </summary>
internal static class VfsVolumeFactory
{
    private const string ZarExtension = ".zar";

    private const string ChdExtension = ".chd";

    /// <summary>
    /// Opens <paramref name="imagePath"/> as an Xbox ISO, Xbox ISO CHD or ZArchive volume.
    /// </summary>
    /// <param name="imagePath">The path to the image to open.</param>
    /// <param name="exposeImageIso">
    /// When <see langword="true"/>, the volume also exposes the raw Xbox image as a
    /// virtual <c>image.iso</c> file for emulators that only accept disc images.
    /// </param>
    /// <returns>The volume that exposes the image's file system.</returns>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid Xbox ISO, Xbox ISO CHD or ZArchive.</exception>
    public static IVfsVolume Open(string imagePath, bool exposeImageIso = false)
    {
        try
        {
            if (HasExtension(imagePath, ZarExtension))
            {
                return OpenZar(ZarVfsVolume.OpenArchiveOrThrow(imagePath), imagePath, exposeImageIso);
            }

            if (HasExtension(imagePath, ChdExtension))
            {
                return OpenChd(imagePath, exposeImageIso);
            }

            XisoVfsVolume xisoVolume;
            try
            {
                xisoVolume = new XisoVfsVolume(imagePath);
            }
            catch (InvalidImageException)
            {
                // A CHD or ZArchive renamed to .iso (or another extension) should still mount.
                if (Chd.IsChdFile(imagePath))
                {
                    Log.Information("'{ImagePath}' is not an Xbox ISO; opening it as a CHD.", imagePath);
                    return OpenChd(imagePath, exposeImageIso);
                }

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
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to expose '{ImagePath}' as image.iso; cleaning up", imagePath);
                source?.Dispose();
                xisoVolume.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open image '{ImagePath}'", imagePath);
            throw;
        }
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
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to expose the embedded XISO of '{ArchivePath}'", archivePath);
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
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to synthesize image.iso for '{ArchivePath}'", archivePath);
            virtualSource?.Dispose();
            treeVolume.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an Xbox ISO CHD. The decompressed image must parse as XDVDFS; when
    /// requested, a second independent CHD reader backs the virtual <c>image.iso</c>
    /// so raw-image reads never race the volume's stream (CHD streams are not
    /// thread-safe).
    /// </summary>
    private static IVfsVolume OpenChd(string imagePath, bool exposeImageIso)
    {
        var xisoVolume = OpenChdVolume(imagePath);
        if (!exposeImageIso)
        {
            return xisoVolume;
        }

        IRawImageSource? source = null;
        try
        {
            source = new StreamRawImageSource(ChdImageSource.OpenOrThrow(imagePath));
            return new ImageIsoVfsVolume(xisoVolume, source);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to expose CHD '{ImagePath}' as image.iso", imagePath);
            source?.Dispose();
            xisoVolume.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens the decompressed CHD image as an <see cref="XisoVfsVolume"/>, mapping a
    /// failed XDVDFS parse to a CHD-specific error message. The volume takes ownership
    /// of the stream; a failed parse disposes it inside the volume constructor.
    /// </summary>
    private static XisoVfsVolume OpenChdVolume(string imagePath)
    {
        var stream = ChdImageSource.OpenOrThrow(imagePath);
        try
        {
            return new XisoVfsVolume(stream, imagePath);
        }
        catch (InvalidImageException ex)
        {
            Log.Debug(ex, "CHD '{ImagePath}' does not contain an Xbox ISO image", imagePath);
            throw new InvalidImageException(
                $"'{imagePath}' is not an Xbox ISO CHD (XDVDFS filesystem not found).", ex);
        }
    }

    /// <summary>
    /// Opens the raw image bytes for a path-based image: CISO inputs are decompressed
    /// on demand through the block device, plain ISO/XISO files are read directly.
    /// </summary>
    private static IRawImageSource OpenPathRawImageSource(string imagePath)
    {
        return new StreamRawImageSource(XisoReader.OpenImageStream(imagePath, FileShare.ReadWrite));
    }

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