using System.Net;
using System.Reflection;
using System.Text.Json;
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
    /// Verifies waiting for the launch report completes immediately when nothing is pending.
    /// </summary>
    [Fact]
    public async Task WaitForPendingReportAsync_WhenIdle_Completes()
    {
        await StatsService.WaitForPendingReportAsync(TimeSpan.FromMilliseconds(50))
            .WaitAsync(TimeSpan.FromSeconds(2));
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

    /// <summary>
    /// Verifies a server error is swallowed (stats reporting is advisory).
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_WithServerError_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        using var client = new HttpClient(handler);

        await StatsService.ReportLaunchAsync(client);
    }

    /// <summary>
    /// Verifies a timeout is swallowed (stats reporting is advisory).
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_WhenRequestTimesOut_DoesNotThrow()
    {
        using var client = new HttpClient(new ThrowingHttpMessageHandler(new TaskCanceledException("timeout")));

        await StatsService.ReportLaunchAsync(client);
    }

    /// <summary>
    /// Verifies an unreachable endpoint is swallowed (stats reporting is advisory).
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_WhenEndpointUnreachable_DoesNotThrow()
    {
        using var client = new HttpClient(new ThrowingHttpMessageHandler(new HttpRequestException("offline")));

        await StatsService.ReportLaunchAsync(client);
    }

    /// <summary>
    /// Verifies exactly one request is sent and the body is JSON.
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_SendsSingleJsonRequest()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);

        await StatsService.ReportLaunchAsync(client);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies the reported version matches the entry assembly version.
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_ReportsEntryAssemblyVersion()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);

        await StatsService.ReportLaunchAsync(client);

        using var document = JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var expected = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString()
                       ?? "0.0.0";
        Assert.Equal(expected, document.RootElement.GetProperty("version").GetString());
    }

    /// <summary>
    /// Verifies the API key is sent as a non-empty bearer token.
    /// </summary>
    [Fact]
    public async Task ReportLaunchAsync_SendsNonEmptyBearerToken()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);

        await StatsService.ReportLaunchAsync(client);

        var authorization = Assert.Single(handler.Requests).Headers.Authorization;
        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization.Scheme);
        Assert.False(string.IsNullOrWhiteSpace(authorization.Parameter));
    }
}