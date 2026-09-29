using System.Diagnostics;
using System.Text;
using Serilog;

namespace SimpleXisoDrive;

/// <summary>
/// Builds and prints the Windows usage text.
/// </summary>
internal static class UsageText
{
    /// <summary>
    /// Gets the executable name used in the usage text, derived from the running process.
    /// </summary>
    /// <returns>The executable name without extension, or <c>SimpleXisoDrive</c> when unavailable.</returns>
    internal static string GetExecutableName()
    {
        var mainModule = Process.GetCurrentProcess().MainModule;
        var name = mainModule != null ? Path.GetFileNameWithoutExtension(mainModule.FileName) : null;
        return string.IsNullOrEmpty(name) ? "SimpleXisoDrive" : name;
    }

    /// <summary>
    /// Gets the usage text for the supplied executable name.
    /// </summary>
    /// <param name="exeName">The executable name to show in the usage line.</param>
    /// <returns>The full usage text.</returns>
    internal static string GetUsage(string exeName)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "Mounts an Xbox ISO/XISO (.iso, .xiso), Xbox ISO CHD (.chd) or ZArchive (.zar) file as a virtual file system on Windows.");
        builder.AppendLine();
        builder.AppendLine($"Usage: {exeName} <image-file> <mount-path> [options]");
        builder.AppendLine();
        builder.AppendLine("Arguments:");
        builder.AppendLine(
            "  <image-file>    Path to the Xbox image (.iso, .xiso, .cso, .chd) or ZArchive (.zar) file to mount.");
        builder.AppendLine("  <mount-path>    Drive letter (\"M:\\\") or folder path on an NTFS partition.");
        builder.AppendLine();
        builder.AppendLine("Options:");
        builder.AppendLine("  -d, --debug     Display debug Dokan output in the console window.");
        builder.AppendLine("  -l, --launch    Open Windows Explorer to the mount path after mounting.");
        builder.AppendLine("  -i, --image-iso Also expose the raw Xbox image as image.iso at the mount root");
        builder.AppendLine("                  (for emulators such as xemu; ZArchive trees are synthesized).");
        return builder.ToString();
    }

    /// <summary>
    /// Prints the usage text. Failures are logged instead of thrown.
    /// </summary>
    public static void Print()
    {
        try
        {
            Console.WriteLine(GetUsage(GetExecutableName()));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print usage information");
        }
    }
}
