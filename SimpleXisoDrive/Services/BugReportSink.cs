using Serilog.Core;
using Serilog.Events;

namespace SimpleXisoDrive.Services;

/// <summary>
/// Serilog sink that forwards Warning (and above) events to the BugReport API
/// and to the local error.log file. Sink failures are swallowed so logging
/// can never crash the application or re-enter the logging pipeline.
/// </summary>
public sealed class BugReportSink : ILogEventSink
{
    // The remote API allows 10 requests/minute per IP; stay below that.
    private const int MaxReportsPerMinute = 8;

    private static readonly Lock RateLimitLock = new();
    private static readonly Queue<DateTimeOffset> RecentReports = new();

    /// <summary>
    /// Processes a Serilog event, forwarding Warning and higher levels to the local
    /// error log and the remote bug report API.
    /// </summary>
    /// <param name="logEvent">The Serilog event to process.</param>
    public void Emit(LogEvent logEvent)
    {
        try
        {
            if (logEvent.Level < LogEventLevel.Warning) return;
            if (!RateLimitAllows()) return;

            var exception = logEvent.Exception;
            var report = BugReport.BuildReport(logEvent.Level.ToString(), logEvent.RenderMessage(), exception);

            BugReport.WriteLocalErrorLog(report);

            _ = Task.Run(async () =>
            {
                try
                {
                    await BugReport.SendToApiAsync(report, exception?.ToString() ?? "No exception attached.");
                }
                catch
                {
                    // Never let a reporting failure surface through the sink
                }
            });
        }
        catch
        {
            // Sinks must never throw
        }
    }

    private static bool RateLimitAllows()
    {
        lock (RateLimitLock)
        {
            var now = DateTimeOffset.Now;
            while (RecentReports.Count > 0 && now - RecentReports.Peek() > TimeSpan.FromMinutes(1))
            {
                RecentReports.Dequeue();
            }

            if (RecentReports.Count >= MaxReportsPerMinute) return false;

            RecentReports.Enqueue(now);
            return true;
        }
    }
}
