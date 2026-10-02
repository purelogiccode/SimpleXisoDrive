namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Dokan download warning text.
/// </summary>
public class DokanDownloadPromptTests
{
    /// <summary>
    /// Verifies the warning names the missing component, points at the Dokan releases
    /// page and asks whether the page should be opened.
    /// </summary>
    [Fact]
    public void BuildMessage_ContainsComponentUrlAndQuestion()
    {
        var message = DokanDownloadPrompt.BuildMessage("driver (dokan2.sys)",
            "https://example.invalid/dokan");

        Assert.Contains("driver (dokan2.sys)", message, StringComparison.Ordinal);
        Assert.Contains("https://example.invalid/dokan", message, StringComparison.Ordinal);
        Assert.Contains("open the Dokan download page", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the default download URL points at the Dokan releases page.
    /// </summary>
    [Fact]
    public void ReleasesUrl_PointsAtDokanReleasesPage()
    {
        Assert.Equal("https://github.com/dokan-dev/dokany/releases", DokanDownloadPrompt.ReleasesUrl);
    }

    /// <summary>
    /// Verifies the warning explains the install-then-rerun next step.
    /// </summary>
    [Fact]
    public void BuildMessage_ExplainsInstallAndRerun()
    {
        var message = DokanDownloadPrompt.BuildMessage("driver (dokan2.sys)", DokanDownloadPrompt.ReleasesUrl);

        Assert.Contains("Install it", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("re-run SimpleXisoDrive", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("free and open-source", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the release page is offered over HTTPS.
    /// </summary>
    [Fact]
    public void ReleasesUrl_UsesHttps()
    {
        Assert.StartsWith("https://", DokanDownloadPrompt.ReleasesUrl, StringComparison.Ordinal);
    }
}
