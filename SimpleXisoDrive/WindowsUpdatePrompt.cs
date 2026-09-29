using System.Runtime.InteropServices;

namespace SimpleXisoDrive;

/// <summary>
/// Notifies the user about a new release with a native Windows message box and asks
/// whether the release page should be opened to download it.
/// </summary>
internal static class WindowsUpdatePrompt
{
    private const uint MbYesNo = 0x00000004;
    private const uint MbIconInformation = 0x00000040;
    private const uint MbSetForeground = 0x00010000;
    private const uint MbTopmost = 0x00040000;
    private const int IdYes = 6;

    private const string Caption = "SimpleXisoDrive Update";

    /// <summary>
    /// Shows the update message box and returns whether the user chose to open the
    /// release page. When the process has no interactive console (scripts, scheduled
    /// tasks and CI runs), no window is shown: the release URL is printed instead and
    /// the prompt reports "no" so the run can never block on a modal dialog.
    /// </summary>
    /// <param name="current">The running version.</param>
    /// <param name="latest">The latest available version.</param>
    /// <param name="releaseUrl">The release page URL offered for the download.</param>
    /// <returns><see langword="true"/> when the user accepts; otherwise <see langword="false"/>.</returns>
    public static bool ConfirmOpenRelease(Version current, Version latest, string releaseUrl)
    {
        if (!IsInteractive)
        {
            Console.WriteLine($"A newer version of SimpleXisoDrive is available ({latest}).");
            Console.WriteLine($"Download it from: {releaseUrl}");
            return false;
        }

        var result = MessageBoxW(IntPtr.Zero, BuildMessage(current, latest, releaseUrl), Caption,
            MbYesNo | MbIconInformation | MbSetForeground | MbTopmost);
        return result == IdYes;
    }

    /// <summary>
    /// Gets a value indicating whether the process can show a blocking message box.
    /// </summary>
    private static bool IsInteractive
    {
        get
        {
            try
            {
                return ShouldUseMessageBox(Environment.UserInteractive, Console.IsInputRedirected,
                    Console.IsOutputRedirected);
            }
            catch (IOException)
            {
                // No usable console handles: treat the run as non-interactive.
                return false;
            }
        }
    }

    /// <summary>
    /// Decides whether the update notification may use a message box.
    /// </summary>
    /// <param name="userInteractive">Whether the process owns an interactive desktop session.</param>
    /// <param name="inputRedirected">Whether standard input is redirected.</param>
    /// <param name="outputRedirected">Whether standard output is redirected.</param>
    /// <returns><see langword="true"/> when a message box can be shown safely.</returns>
    internal static bool ShouldUseMessageBox(bool userInteractive, bool inputRedirected, bool outputRedirected)
    {
        return userInteractive && !inputRedirected && !outputRedirected;
    }

    /// <summary>
    /// Builds the message box text for the available update.
    /// </summary>
    /// <param name="current">The running version.</param>
    /// <param name="latest">The latest available version.</param>
    /// <param name="releaseUrl">The release page URL.</param>
    /// <returns>The message text.</returns>
    internal static string BuildMessage(Version current, Version latest, string releaseUrl)
    {
        return $"A newer version of SimpleXisoDrive is available.{Environment.NewLine}{Environment.NewLine}" +
               $"Current version: {current}{Environment.NewLine}" +
               $"Latest version:  {latest}{Environment.NewLine}{Environment.NewLine}" +
               $"Do you want to open the release page to download it?{Environment.NewLine}{Environment.NewLine}" +
               releaseUrl;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
