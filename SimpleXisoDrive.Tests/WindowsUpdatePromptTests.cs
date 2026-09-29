namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Windows update message text.
/// </summary>
public class WindowsUpdatePromptTests
{
    /// <summary>
    /// Verifies the message contains both versions and the release URL.
    /// </summary>
    [Fact]
    public void BuildMessage_ContainsVersionsAndReleaseUrl()
    {
        var message = WindowsUpdatePrompt.BuildMessage(new Version(1, 2, 3, 4), new Version(2, 0, 0),
            "https://example.invalid/release");

        Assert.Contains("1.2.3.4", message, StringComparison.Ordinal);
        Assert.Contains("2.0.0", message, StringComparison.Ordinal);
        Assert.Contains("https://example.invalid/release", message, StringComparison.Ordinal);
        Assert.Contains("open the release page", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the message box is used only for interactive runs; redirected or
    /// non-interactive runs must never block on a modal dialog.
    /// </summary>
    /// <param name="userInteractive">Whether the process is interactive.</param>
    /// <param name="inputRedirected">Whether standard input is redirected.</param>
    /// <param name="outputRedirected">Whether standard output is redirected.</param>
    /// <param name="expected">The expected decision.</param>
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void ShouldUseMessageBox_OnlyForInteractiveRuns(bool userInteractive, bool inputRedirected,
        bool outputRedirected, bool expected)
    {
        Assert.Equal(expected,
            WindowsUpdatePrompt.ShouldUseMessageBox(userInteractive, inputRedirected, outputRedirected));
    }
}
