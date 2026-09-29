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
    private static readonly string[] ImageExtensions = [".iso", ".xiso", ".cso", ".zar"];

    /// <summary>
    /// Resolves the image file path, handling cases where the user provides a path without an
    /// extension. Supports Xbox ISO/XISO images (<c>.iso</c>, <c>.xiso</c>), CISO-compressed
    /// images (<c>.cso</c>, including split <c>.1.cso</c> sets) and ZArchive (<c>.zar</c>)
    /// files. Tries multiple strategies to find the file:
    /// 1. Return original path if file exists
    /// 2. If path is a directory containing exactly one image file, resolve to it
    /// 3. If no extension, try appending each supported extension
    /// 4. If just a filename, try looking in current directory
    /// </summary>
    /// <param name="imagePath">The image path supplied by the user.</param>
    /// <returns>The resolved image file path, or <see langword="null"/> when no file matches.</returns>
    public static string? Resolve(string imagePath)
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
            foreach (var candidate in EnumerateExtensionCandidates(imagePath))
            {
                if (File.Exists(candidate))
                {
                    Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, candidate);
                    return candidate;
                }
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
                foreach (var candidate in EnumerateExtensionCandidates(inCurrentDir))
                {
                    if (File.Exists(candidate))
                    {
                        Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, candidate);
                        return candidate;
                    }
                }
            }
        }

        // File not found
        return null;
    }

    private static List<string> FindImageFiles(string directory)
    {
        var imageFiles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in ImageExtensions)
        {
            foreach (var file in Directory.GetFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly))
            {
                // A split CISO set (game.1.cso, game.2.cso, ...) is one image; the
                // entry point is the first part, so continuation parts are ignored.
                if (string.Equals(extension, ".cso", StringComparison.OrdinalIgnoreCase) && IsCsoContinuationPart(file))
                {
                    continue;
                }

                if (seen.Add(file))
                {
                    imageFiles.Add(file);
                }
            }
        }

        return imageFiles;
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