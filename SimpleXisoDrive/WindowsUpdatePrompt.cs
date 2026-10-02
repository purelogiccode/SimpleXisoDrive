namespace SimpleXisoDrive;

/// <summary>
/// Notifies the user about a new release with a native Windows message box and asks
/// whether the release page should be opened to download it.
/// </summary>
internal static class WindowsUpdatePrompt
{
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
        if (!WindowsMessageBox.IsInteractive)
        {
            Console.WriteLine($"A newer version of SimpleXisoDrive is available ({latest}).");
            Console.WriteLine($"Download it from: {releaseUrl}");
            return false;
        }

        return WindowsMessageBox.Confirm(BuildMessage(current, latest, releaseUrl), Caption);
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
}