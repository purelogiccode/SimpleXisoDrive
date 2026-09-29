using Serilog;

namespace SimpleXisoDrive.Fuse;

/// <summary>
/// Verifies that the FUSE 3 runtime needed for mounting is present and prints
/// installation guidance when it is not.
/// </summary>
internal static class FuseAvailability
{
    /// <summary>
    /// Checks whether the FUSE 3 library and kernel support are available.
    /// </summary>
    /// <param name="libraryPath">When this method returns, the FUSE library that was found.</param>
    /// <returns><see langword="true"/> when mounting can be attempted; otherwise <see langword="false"/>.</returns>
    public static bool Check(out string? libraryPath)
    {
        FuseInterop.RegisterResolver();

        if (!FuseInterop.TryLoadLibrary(out libraryPath))
        {
            PrintMissingLibraryInstructions();
            return false;
        }

        Log.Information("FUSE library loaded: {LibraryPath}", libraryPath);

        try
        {
            Log.Information("FUSE API version: {FuseVersion}", FuseInterop.FuseVersion());
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not query the FUSE API version");
        }

        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        if (!File.Exists("/dev/fuse"))
        {
            Console.Error.WriteLine("Error: /dev/fuse was not found, so FUSE mounts cannot work.");
            Console.Error.WriteLine("Load the FUSE kernel module (for example: sudo modprobe fuse) and re-run.");
            Log.Error("FUSE check failed: /dev/fuse is missing.");
            return false;
        }

        if (!Environment.IsPrivilegedProcess && !IsOnPath("fusermount3"))
        {
            Console.WriteLine("Warning: fusermount3 was not found on PATH. Mounting may fail.");
            Console.WriteLine("Install the FUSE tools package (for example: sudo apt install fuse3).");
            Log.Warning("fusermount3 not found on PATH; mounting may fail.");
        }

        return true;
    }

    private static bool IsOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, executable)))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return false;
    }

    private static void PrintMissingLibraryInstructions()
    {
        Console.Error.WriteLine();

        if (OperatingSystem.IsMacOS())
        {
            Console.Error.WriteLine("Error: macFUSE (libfuse3) was not found.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("SimpleXisoDrive needs macFUSE to mount images on macOS.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("To fix this:");
            Console.Error.WriteLine("  1. Download and install macFUSE from: https://macfuse.io");
            Console.Error.WriteLine("  2. On macOS 15.4 or later, choose the FSKit backend when prompted");
            Console.Error.WriteLine("     (no kernel extension required).");
            Console.Error.WriteLine("  3. Re-run SimpleXisoDrive.");
            Log.Error("FUSE check FAILED: macFUSE libfuse3 was not found.");
        }
        else
        {
            Console.Error.WriteLine("Error: the FUSE 3 library (libfuse3) was not found.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("SimpleXisoDrive needs FUSE 3 to mount images on Linux.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("To fix this, install FUSE 3 with your package manager:");
            Console.Error.WriteLine("  Debian/Ubuntu: sudo apt install libfuse3-3 fuse3");
            Console.Error.WriteLine("  Fedora:        sudo dnf install fuse3 fuse3-libs");
            Console.Error.WriteLine("  Arch:          sudo pacman -S fuse3");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Then re-run SimpleXisoDrive.");
            Log.Error("FUSE check FAILED: libfuse3 was not found.");
        }

        Console.Error.WriteLine();
    }
}
