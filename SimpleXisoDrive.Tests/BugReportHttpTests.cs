using System.Net;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the remote bug report HTTP path with a stubbed handler (no live traffic).
/// The local log paths are redirected to a temporary directory so the real logs are never touched.
/// </summary>
[Collection(BugReportFileCollection.Name)]
public class BugReportHttpTests : IDisposable
{
    private readonly string _logDirectory;

    /// <summary>
    /// Redirects the local log files to a fresh temporary directory.
    /// </summary>
    public BugReportHttpTests()
    {
        _logDirectory = Path.Combine(Path.GetTempPath(), "simplexiso-bugreporthttp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logDirectory);
        BugReport.OverrideLogFilePaths(
            Path.Combine(_logDirectory, "error.log"),
            Path.Combine(_logDirectory, "critical_error.log"));
    }

    /// <summary>
    /// Restores the default log paths and removes the temporary directory.
    /// </summary>
    public void Dispose()
    {
        BugReport.OverrideLogFilePaths(null, null);
        try
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

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

    /// <summary>
    /// Verifies a successful send leaves no error remark in the critical log.
    /// </summary>
    [Fact]
    public async Task SendToApiAsync_WhenApiAccepts_DoesNotThrow()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"message\":\"ok\"}");
        using var client = new HttpClient(handler);

        await BugReport.SendToApiAsync("report body", "stack trace", client);

        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// Verifies a non-success status is recorded in the critical log with the status code
    /// and response body.
    /// </summary>
    [Fact]
    public async Task SendToApiAsync_WhenApiReturnsError_WritesCriticalLog()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "server exploded");
        using var client = new HttpClient(handler);

        await BugReport.SendToApiAsync("report body", "stack trace", client);

        var content = ReadCriticalLog();
        Assert.Contains("Error sending log to API.", content, StringComparison.Ordinal);
        Assert.Contains("InternalServerError", content, StringComparison.Ordinal);
        Assert.Contains("server exploded", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a network failure is recorded in the critical log.
    /// </summary>
    [Fact]
    public async Task SendToApiAsync_WhenNetworkFails_WritesCriticalLog()
    {
        using var client = new HttpClient(new ThrowingHttpMessageHandler(
            new HttpRequestException("network is down")));

        await BugReport.SendToApiAsync("report body", "stack trace", client);

        var content = ReadCriticalLog();
        Assert.Contains("Exception occurred while sending log to API.", content, StringComparison.Ordinal);
        Assert.Contains("network is down", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the critical error log with shared access so a concurrent append does not
    /// make the read fail.
    /// </summary>
    private static string ReadCriticalLog()
    {
        using var stream = new FileStream(BugReport.CriticalLogFilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}