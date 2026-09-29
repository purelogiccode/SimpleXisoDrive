using Serilog.Core;
using Serilog.Events;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Serilog sink's guard behavior. Warning-and-above events are not emitted here because
/// they would forward to the live bug report API.
/// </summary>
public class BugReportSinkTests
{
    /// <summary>
    /// Verifies events below the Warning threshold are ignored without side effects.
    /// </summary>
    [Fact]
    public void Emit_WithEventBelowWarning_IsIgnored()
    {
        var sink = new BugReportSink();
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Debug, null, MessageTemplate.Empty, []);

        sink.Emit(logEvent);
    }

    /// <summary>
    /// Verifies a malformed event cannot escape the sink.
    /// </summary>
    [Fact]
    public void Emit_WithNullEvent_IsSwallowed()
    {
        var sink = new BugReportSink();

        Assert.Null(Record.Exception(() => sink.Emit(null!)));
    }
}
