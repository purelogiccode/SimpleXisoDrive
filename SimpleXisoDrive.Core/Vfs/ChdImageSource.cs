using CHDSharp;
using CHDSharp.Models;
using Serilog;

namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// Opens Xbox ISO CHD files through the CHDSharp library and exposes them as a
/// seekable stream over the decompressed image. Only Xbox ISO content is accepted:
/// CD and GD-ROM CHDs are rejected up front because their 2352-byte sectors do not
/// carry an XDVDFS filesystem, and the decompressed image must parse as XDVDFS
/// before a mount succeeds.
/// </summary>
internal static class ChdImageSource
{
    /// <summary>
    /// Opens <paramref name="chdPath"/> and returns a seekable, read-only stream over
    /// the decompressed image. The stream owns the CHD reader; disposing it releases
    /// the underlying file handle.
    /// </summary>
    /// <param name="chdPath">The path to the CHD file to open.</param>
    /// <returns>The decompressed image as a seekable stream.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the CHD file does not exist.</exception>
    /// <exception cref="InvalidImageException">
    /// Thrown when the file is not a readable CHD, is a CD/GD-ROM CHD, or is a
    /// differential child that requires a parent.
    /// </exception>
    public static ChdImageStream OpenOrThrow(string chdPath)
    {
        try
        {
            var error = ChdFile.Open(chdPath, out var chd);
            if (error != ChdError.Chderrnone || chd is null)
            {
                Log.Debug("CHD open failed for '{ImagePath}': {Error}", chdPath, error);
                if (error == ChdError.Chderrfilenotfound)
                {
                    throw new FileNotFoundException($"CHD file not found: '{chdPath}'", chdPath);
                }

                throw new InvalidImageException($"'{chdPath}' is not a readable CHD file: {error.GetMessage()}");
            }

            try
            {
                if (chd.IsCd || chd.IsGdRom)
                {
                    var kind = chd.IsGdRom ? "GD-ROM" : "CD";
                    Log.Debug("Rejecting {Kind} CHD '{ImagePath}'", kind, chdPath);
                    throw new InvalidImageException(
                        $"'{chdPath}' is a {kind} CHD; only Xbox ISO CHDs are supported.");
                }

                var streamError = ChdFile.OpenAsStream(chd, out var stream);
                if (streamError != ChdError.Chderrnone || stream is null)
                {
                    throw new InvalidImageException($"Failed to open CHD image: {streamError.GetMessage()}");
                }

                return stream;
            }
            catch
            {
                chd.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            // The factory logs the user-visible error; keep the full detail in the debug log.
            Log.Debug(ex, "Failed to open CHD image '{ImagePath}'", chdPath);
            throw;
        }
    }
}