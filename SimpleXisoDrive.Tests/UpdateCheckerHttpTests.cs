using System.Net;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the update-check HTTP path with a stubbed handler (no live traffic). The canned
/// tag is older than any host version, so the prompt is never reached.
/// </summary>
public class UpdateCheckerHttpTests
{
    /// <summary>
    /// Verifies the latest-release endpoint is queried with the update-checker user agent.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_SendsRequestWithUserAgent()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            "{\"tag_name\":\"v0.0.0\",\"html_url\":\"https://example.invalid/release\"}");
        using var client = new HttpClient(handler);

        await UpdateChecker.CheckForUpdateAsync(client);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/purelogiccode/SimpleXisoDrive/releases/latest",
            request.RequestUri?.ToString());
        Assert.Contains("SimpleXisoDrive-UpdateChecker", Assert.Single(handler.UserAgents), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies an available update is offered to the supplied prompt with the parsed
    /// versions and release URL.
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_WhenUpdateAvailable_InvokesPrompt()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            "{\"tag_name\":\"v999.0.0\",\"html_url\":\"https://example.invalid/release\"}");
        using var client = new HttpClient(handler);
        Version? promptedCurrent = null;
        Version? promptedLatest = null;
        string? promptedUrl = null;

        await UpdateChecker.CheckForUpdateAsync(client, (current, latest, url) =>
        {
            promptedCurrent = current;
            promptedLatest = latest;
            promptedUrl = url;
            return false;
        });

        Assert.Equal(new Version(999, 0, 0), promptedLatest);
        Assert.NotNull(promptedCurrent);
        Assert.Equal("https://example.invalid/release", promptedUrl);
    }

    /// <summary>
    /// Verifies a malformed response body is swallowed (update checks are non-fatal).
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_WithMalformedJson_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "not json");
        using var client = new HttpClient(handler);

        await UpdateChecker.CheckForUpdateAsync(client);
    }

    /// <summary>
    /// Verifies an error status is swallowed (update checks are non-fatal).
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsync_WithErrorStatus_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        using var client = new HttpClient(handler);

        await UpdateChecker.CheckForUpdateAsync(client);
    }
}