namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the native Windows message box interactivity decision.
/// </summary>
public class WindowsMessageBoxTests
{
    /// <summary>
    /// Verifies a message box is allowed only for interactive runs; redirected or
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
    public void ShouldShow_OnlyForInteractiveRuns(bool userInteractive, bool inputRedirected,
        bool outputRedirected, bool expected)
    {
        Assert.Equal(expected, WindowsMessageBox.ShouldShow(userInteractive, inputRedirected, outputRedirected));
    }
}
