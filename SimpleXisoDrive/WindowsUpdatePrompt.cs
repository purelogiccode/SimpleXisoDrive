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
    /// release page.
    /// </summary>
    /// <param name="current">The running version.</param>
    /// <param name="latest">The latest available version.</param>
    /// <param name="releaseUrl">The release page URL offered for the download.</param>
    /// <returns><see langword="true"/> when the user accepts; otherwise <see langword="false"/>.</returns>
    public static bool ConfirmOpenRelease(Version current, Version latest, string releaseUrl)
    {
        var result = MessageBoxW(IntPtr.Zero, BuildMessage(current, latest, releaseUrl), Caption,
            MbYesNo | MbIconInformation | MbSetForeground | MbTopmost);
        return result == IdYes;
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
