using System.Net;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the remote bug report HTTP path with a stubbed handler (no live traffic).
/// </summary>
public class BugReportHttpTests
{
    /// <summary>
    /// Verifies the report is posted to the API with the key header and JSON payload.
    /// </summary>
    [Fact]
    public async Task SendToApiAsync_PostsReportWithApiKeyHeader()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);

        await BugReport.SendToApiAsync("report body", "stack trace", client);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://www.purelogiccode.com/bugreport/api/send-bug-report", request.RequestUri?.ToString());
        Assert.True(request.Headers.Contains("X-API-KEY"));

        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"message\":\"report body\"", body, StringComparison.Ordinal);
        Assert.Contains("\"applicationName\":\"SimpleXisoDrive\"", body, StringComparison.Ordinal);
        Assert.Contains("\"stackTrace\":\"stack trace\"", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies waiting for in-flight reports completes immediately when nothing is pending.
    /// </summary>
    [Fact]
    public async Task WaitForPendingReportsAsync_WhenIdle_Completes()
    {
        Assert.Equal(0, BugReport.PendingReports);

        await BugReport.WaitForPendingReportsAsync(TimeSpan.FromMilliseconds(50))
            .WaitAsync(TimeSpan.FromSeconds(2));
    }
}
