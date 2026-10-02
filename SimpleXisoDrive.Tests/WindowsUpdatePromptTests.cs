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
}