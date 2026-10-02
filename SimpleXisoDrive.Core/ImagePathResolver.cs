using Serilog;

namespace SimpleXisoDrive.Core;

/// <summary>
/// Resolves user-supplied image paths to actual image files, handling directories,
/// missing extensions and current-directory lookups. Shared by the Windows and Unix
/// front ends.
/// </summary>
public static class ImagePathResolver
{
    /// <summary>
    /// The file extensions the resolver recognizes, in preference order.
    /// </summary>
    private static readonly string[] ImageExtensions = [".iso", ".xiso", ".cso", ".chd", ".zar"];

    /// <summary>
    /// Resolves the image file path, handling cases where the user provides a path without an
    /// extension. Supports Xbox ISO/XISO images (<c>.iso</c>, <c>.xiso</c>), CISO-compressed
    /// images (<c>.cso</c>, including split <c>.1.cso</c> sets), Xbox ISO CHD images
    /// (<c>.chd</c>) and ZArchive (<c>.zar</c>) files. Tries multiple strategies to find the file:
    /// 1. Return original path if file exists
    /// 2. If path is a directory containing exactly one image file, resolve to it
    /// 3. If no extension, try appending each supported extension
    /// 4. If just a filename, try looking in current directory
    /// </summary>
    /// <param name="imagePath">The image path supplied by the user.</param>
    /// <returns>The resolved image file path, or <see langword="null"/> when no file matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="imagePath"/> is <see langword="null"/>.</exception>
    public static string? Resolve(string imagePath)
    {
        ArgumentNullException.ThrowIfNull(imagePath);

        try
        {
            return ResolveCore(imagePath);
        }
        catch (Exception ex)
        {
            // The front end reports the user-visible failure; keep the detail in the
            // debug log so one failure is not reported twice.
            Log.Debug(ex, "Failed to resolve image path '{ImagePath}'", imagePath);
            throw;
        }
    }

    private static string? ResolveCore(string imagePath)
    {
        // 1. Check if the file exists as-is
        if (File.Exists(imagePath))
        {
            return imagePath;
        }

        // 2. If the path is a directory, look for a single image file inside it
        if (Directory.Exists(imagePath))
        {
            try
            {
                var imageFiles = FindImageFiles(imagePath);
                switch (imageFiles.Count)
                {
                    case 1:
                        Log.Debug("Resolved directory '{ImagePath}' to image file '{Resolved}'", imagePath,
                            imageFiles[0]);
                        return imageFiles[0];
                    case > 1:
                        Log.Debug(
                            "Directory '{ImagePath}' contains multiple image files; cannot auto-resolve.", imagePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error scanning directory '{ImagePath}' for image files", imagePath);
            }
        }

        // 3. If no extension provided, try appending each supported extension
        if (string.IsNullOrEmpty(Path.GetExtension(imagePath)))
        {
            var candidate = FindFileWithImageExtension(imagePath);
            if (candidate is not null)
            {
                Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, candidate);
                return candidate;
            }
        }

        // 4. If it's just a filename (no path), try looking in current directory
        if (!imagePath.Contains(Path.DirectorySeparatorChar) &&
            !imagePath.Contains(Path.AltDirectorySeparatorChar))
        {
            var inCurrentDir = Path.Combine(Environment.CurrentDirectory, imagePath);
            if (File.Exists(inCurrentDir))
            {
                Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, inCurrentDir);
                return inCurrentDir;
            }

            // Also try each supported extension in the current directory
            if (string.IsNullOrEmpty(Path.GetExtension(imagePath)))
            {
                var candidate = FindFileWithImageExtension(inCurrentDir);
                if (candidate is not null)
                {
                    Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, candidate);
                    return candidate;
                }
            }
        }

        // File not found
        return null;
    }

    private static List<string> FindImageFiles(string directory)
    {
        var imageFiles = new List<string>();

        foreach (var extension in ImageExtensions)
        {
            foreach (var file in EnumerateFilesByExtension(directory, extension))
            {
                // A split CISO set (game.1.cso, game.2.cso, ...) is one image; the
                // entry point is the first part, so continuation parts are ignored.
                if (string.Equals(extension, ".cso", StringComparison.OrdinalIgnoreCase) && IsCsoContinuationPart(file))
                {
                    continue;
                }

                imageFiles.Add(file);
            }
        }

        return imageFiles;
    }

    private static IEnumerable<string> EnumerateFilesByExtension(string directory, string extension)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Finds the image file matching <paramref name="path"/> plus one of the supported
    /// extensions, preferring the exact candidate and then falling back to a
    /// case-insensitive directory scan so case-sensitive file systems (Linux, macOS)
    /// resolve, for example, <c>GAME</c> to <c>GAME.CHD</c>.
    /// </summary>
    private static string? FindFileWithImageExtension(string path)
    {
        foreach (var candidate in EnumerateExtensionCandidates(path))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Length == 0)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            directory = ".";
        }

        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var matches = new List<string>();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(Path.GetFileNameWithoutExtension(file), fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    matches.Add(file);
                }
            }

            // Preserve the documented extension preference order.
            foreach (var extension in ImageExtensions)
            {
                foreach (var match in matches)
                {
                    if (string.Equals(Path.GetExtension(match), extension, StringComparison.OrdinalIgnoreCase))
                    {
                        return match;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error scanning directory for image candidates of '{ImagePath}'", path);
            return null;
        }
    }

    private static bool IsCsoContinuationPart(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var separator = name.LastIndexOf('.');
        if (separator < 0 || separator == name.Length - 1)
        {
            return false;
        }

        return int.TryParse(name.AsSpan(separator + 1), System.Globalization.CultureInfo.InvariantCulture,
            out var part) && part >= 2;
    }

    private static IEnumerable<string> EnumerateExtensionCandidates(string path)
    {
        foreach (var extension in ImageExtensions)
        {
            yield return path + extension;
        }
    }
}