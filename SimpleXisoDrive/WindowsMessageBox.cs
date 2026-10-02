using System.Runtime.InteropServices;

namespace SimpleXisoDrive;

/// <summary>
/// Native Windows message box used by the front end for interactive prompts. The
/// interactivity check keeps scripts, scheduled tasks and CI runs from blocking on
/// a modal dialog.
/// </summary>
internal static class WindowsMessageBox
{
    private const uint MbYesNo = 0x00000004;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbIconInformation = 0x00000040;
    private const uint MbSetForeground = 0x00010000;
    private const uint MbTopmost = 0x00040000;
    private const int IdYes = 6;

    /// <summary>
    /// Gets a value indicating whether the process can safely show a blocking message box.
    /// </summary>
    public static bool IsInteractive
    {
        get
        {
            try
            {
                return ShouldShow(Environment.UserInteractive, Console.IsInputRedirected, Console.IsOutputRedirected);
            }
            catch (IOException)
            {
                // No usable console handles: treat the run as non-interactive.
                return false;
            }
        }
    }

    /// <summary>
    /// Decides whether a message box may be shown.
    /// </summary>
    /// <param name="userInteractive">Whether the process owns an interactive desktop session.</param>
    /// <param name="inputRedirected">Whether standard input is redirected.</param>
    /// <param name="outputRedirected">Whether standard output is redirected.</param>
    /// <returns><see langword="true"/> when a message box can be shown safely.</returns>
    internal static bool ShouldShow(bool userInteractive, bool inputRedirected, bool outputRedirected)
    {
        return userInteractive && !inputRedirected && !outputRedirected;
    }

    /// <summary>
    /// Shows a modal Yes/No message box and reports whether the user chose Yes.
    /// </summary>
    /// <param name="text">The message text.</param>
    /// <param name="caption">The window title.</param>
    /// <param name="warningIcon">Whether to show the warning icon instead of the information icon.</param>
    /// <returns><see langword="true"/> when the user clicked Yes; otherwise <see langword="false"/>.</returns>
    public static bool Confirm(string text, string caption, bool warningIcon = false)
    {
        var type = MbYesNo | MbSetForeground | MbTopmost | (warningIcon ? MbIconWarning : MbIconInformation);
        return MessageBoxW(IntPtr.Zero, text, caption, type) == IdYes;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
