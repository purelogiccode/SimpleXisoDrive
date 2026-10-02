using System.Diagnostics;
using Serilog;

namespace SimpleXisoDrive;

/// <summary>
/// Warns the user that a required Dokan component is missing and offers to open the
/// Dokan download page in the default browser.
/// </summary>
internal static class DokanDownloadPrompt
{
    /// <summary>The Dokan releases page offered for download.</summary>
    public const string ReleasesUrl = "https://github.com/dokan-dev/dokany/releases";

    private const string Caption = "SimpleXisoDrive - Dokan Required";

    /// <summary>
    /// Warns that <paramref name="component"/> is missing and, when the user accepts,
    /// opens the Dokan download page. Non-interactive runs print the URL instead of
    /// showing a dialog, so a script can never block on a modal window.
    /// </summary>
    /// <param name="component">
    /// A description of the missing component, for example <c>"runtime library (dokan2.dll)"</c>.
    /// </param>
    /// <returns><see langword="true"/> when the browser was launched; otherwise <see langword="false"/>.</returns>
    public static bool OfferDownload(string component)
    {
        var message = BuildMessage(component, ReleasesUrl);

        if (!WindowsMessageBox.IsInteractive)
        {
            Console.Error.WriteLine($"The Dokan {component} is required but was not found.");
            Console.Error.WriteLine($"Download Dokan from: {ReleasesUrl}");
            return false;
        }

        if (!WindowsMessageBox.Confirm(message, Caption, warningIcon: true))
        {
            return false;
        }

        return OpenBrowser(ReleasesUrl);
    }

    /// <summary>
    /// Builds the warning text offering the Dokan download page.
    /// </summary>
    /// <param name="component">A description of the missing component.</param>
    /// <param name="releasesUrl">The Dokan download URL.</param>
    /// <returns>The message text.</returns>
    internal static string BuildMessage(string component, string releasesUrl)
    {
        return $"SimpleXisoDrive requires the Dokan {component}, which is not installed."
               + Environment.NewLine + Environment.NewLine
               + "Dokan is free and open-source. Install it, then re-run SimpleXisoDrive."
               + Environment.NewLine + Environment.NewLine
               + "Do you want to open the Dokan download page?" + Environment.NewLine + Environment.NewLine
               + releasesUrl;
    }

    private static bool OpenBrowser(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            Log.Information("Opened the Dokan download page: {Url}", url);
            return true;
        }
        catch (Exception ex)
        {
            // Non-fatal: the URL is printed so the user can open it manually. Logged at
            // Information level so a browser failure is not forwarded as a bug report.
            Log.Information(ex, "Could not open the Dokan download page automatically");
            Console.Error.WriteLine($"Could not launch the browser automatically: {ex.Message}");
            Console.Error.WriteLine($"Download Dokan from: {url}");
            return false;
        }
    }
}
