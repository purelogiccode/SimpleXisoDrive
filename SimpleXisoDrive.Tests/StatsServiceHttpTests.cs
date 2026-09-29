using System.Net;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the launch-statistics HTTP path with a stubbed handler (no live traffic).
/// </summary>
public class StatsServiceHttpTests
{
    /// <summary>
    /// Verifies launch statistics are posted with the bearer token and JSON payload.
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_PostsStatsWithBearerToken()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);

        await StatsService.ReportLaunchAsync(client);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://www.purelogiccode.com/ApplicationStats/stats", request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);

        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"applicationId\":\"simplexisodrive\"", body, StringComparison.Ordinal);
        Assert.Contains("\"version\":", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a rejected request is swallowed (stats reporting is advisory).
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_WhenApiRejects_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "{}");
        using var client = new HttpClient(handler);

        await StatsService.ReportLaunchAsync(client);
    }
}