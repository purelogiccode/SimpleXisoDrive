using Serilog;
using SimpleXisoDrive.Models;

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
    /// Checks the Dokan installation, printing guidance to the console when a component is missing.
    /// </summary>
    /// <returns>The detected installation state.</returns>
    public static DokanInstallationStatus Check()
    {
        try
        {
            return CheckCore();
        }
        catch (Exception ex)
        {
            // The probe itself failed (I/O or ACL error); that is not the same as a
            // confirmed missing installation, so do not trigger the install dialog.
            Log.Debug(ex, "Failed to check whether Dokan is installed");
            return DokanInstallationStatus.Unknown;
        }
    }

    private static DokanInstallationStatus CheckCore()
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
            Console.Error.WriteLine($"  1. Download Dokan from: {DokanDownloadPrompt.ReleasesUrl}");
            Console.Error.WriteLine("  2. Install the package (the default installation includes dokan2.dll)");
            Console.Error.WriteLine("  3. Restart your computer if prompted");
            Console.Error.WriteLine("  4. Re-run SimpleXisoDrive");
            Console.Error.WriteLine("");
            Console.Error.WriteLine($"Expected file location: {LibraryPath}");

            // A missing runtime is an expected user-setup condition, not an
            // application error; log it below the bug-report threshold.
            Log.Information("Dokan is not installed: {DllPath} was not found.", LibraryPath);
            return DokanInstallationStatus.RuntimeMissing;
        }

        if (!sysExists)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Warning: The Dokan driver (dokan2.sys) was not found.");
            Console.Error.WriteLine("Mounting may fail. Please reinstall Dokan if you encounter issues.");
            Log.Information("Dokan driver not installed: {SysPath} was not found.", DriverPath);
            return DokanInstallationStatus.DriverMissing;
        }

        Log.Information("Dokan check passed: {DllPath} found.", LibraryPath);
        return DokanInstallationStatus.Installed;
    }
}
