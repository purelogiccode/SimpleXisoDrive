using Serilog;

namespace SimpleXisoDrive;

/// <summary>
/// Detects whether the Dokan runtime library and driver are installed, displaying
/// guidance when they are missing.
/// </summary>
internal static class DokanInstallation
{
    /// <summary>
    /// Gets the expected path of the Dokan user-mode library (<c>dokan2.dll</c>).
    /// </summary>
    internal static string LibraryPath => Path.Combine(Environment.SystemDirectory, "dokan2.dll");

    /// <summary>
    /// Gets the expected path of the Dokan driver (<c>dokan2.sys</c>).
    /// </summary>
    internal static string DriverPath => Path.Combine(Environment.SystemDirectory, "drivers", "dokan2.sys");

    /// <summary>
    /// Checks whether Dokan is installed, printing guidance to the console when it is not.
    /// </summary>
    /// <returns><see langword="true"/> when <c>dokan2.dll</c> is found; otherwise <see langword="false"/>.</returns>
    public static bool IsInstalled()
    {
        try
        {
            return IsInstalledCore();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check whether Dokan is installed");
            return false;
        }
    }

    private static bool IsInstalledCore()
    {
        var dllExists = File.Exists(LibraryPath);
        var sysExists = File.Exists(DriverPath);

        if (!dllExists)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Error: The Dokan runtime library (dokan2.dll) was not found.");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("SimpleXisoDrive requires the Dokan User-Mode File System Library to operate.");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("To fix this:");
            Console.Error.WriteLine("  1. Download Dokan from: https://github.com/dokan-dev/dokany/releases");
            Console.Error.WriteLine("  2. Install the package (the default installation includes dokan2.dll)");
            Console.Error.WriteLine("  3. Restart your computer if prompted");
            Console.Error.WriteLine("  4. Re-run SimpleXisoDrive");
            Console.Error.WriteLine("");
            Console.Error.WriteLine($"Expected file location: {LibraryPath}");

            Log.Error("Dokan check FAILED: {DllPath} not found.", LibraryPath);
            return false;
        }

        if (!sysExists)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Warning: The Dokan driver (dokan2.sys) was not found.");
            Console.Error.WriteLine("Mounting may fail. Please reinstall Dokan if you encounter issues.");
            Log.Warning("Dokan driver warning: {SysPath} not found.", DriverPath);
        }

        Log.Information("Dokan check passed: {DllPath} found.", LibraryPath);
        return true;
    }
}