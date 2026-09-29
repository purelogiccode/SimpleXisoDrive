using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the DokanNet-to-Serilog logging adapter.
/// </summary>
public class SerilogDokanLoggerTests
{
    /// <summary>
    /// Verifies the adapter reports debug output as enabled when the global logger allows it.
    /// </summary>
    [Fact]
    public void DebugEnabled_ReflectsGlobalLoggerLevel()
    {
        var original = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(new CollectingSink()).CreateLogger();

        try
        {
            Assert.True(new SerilogDokanLogger().DebugEnabled);
        }
        finally
        {
            Log.Logger = original;
        }
    }

    /// <summary>
    /// Verifies every Dokan log level is forwarded with its rendered message.
    /// </summary>
    [Fact]
    public void AllLevels_AreForwardedToSerilog()
    {
        var original = Log.Logger;
        var sink = new CollectingSink();
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            var logger = new SerilogDokanLogger();
            logger.Debug("debug {Value}", 1);
            logger.Info("info {Value}", 2);
            logger.Warn("warn {Value}", 3);
            logger.Error("error {Value}", 4);
            logger.Fatal("fatal {Value}", 5);

            Assert.Contains(sink.Events,
                e => e.Level == LogEventLevel.Debug && string.Equals(e.RenderMessage(), "debug 1", StringComparison.Ordinal));
            Assert.Contains(sink.Events,
                e => e.Level == LogEventLevel.Information && string.Equals(e.RenderMessage(), "info 2", StringComparison.Ordinal));
            Assert.Contains(sink.Events,
                e => e.Level == LogEventLevel.Warning && string.Equals(e.RenderMessage(), "warn 3", StringComparison.Ordinal));
            Assert.Contains(sink.Events,
                e => e.Level == LogEventLevel.Error && string.Equals(e.RenderMessage(), "error 4", StringComparison.Ordinal));
            Assert.Contains(sink.Events,
                e => e.Level == LogEventLevel.Fatal && string.Equals(e.RenderMessage(), "fatal 5", StringComparison.Ordinal));
        }
        finally
        {
            Log.Logger = original;
        }
    }

    /// <summary>
    /// Verifies messages without format arguments are forwarded unchanged.
    /// </summary>
    [Fact]
    public void PlainMessage_IsForwarded()
    {
        var original = Log.Logger;
        var sink = new CollectingSink();
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            new SerilogDokanLogger().Info("plain message");

            Assert.Contains(sink.Events,
                e => string.Equals(e.RenderMessage(), "plain message", StringComparison.Ordinal));
        }
        finally
        {
            Log.Logger = original;
        }
    }

    /// <summary>
    /// A Serilog sink that records the emitted events for assertions.
    /// </summary>
    private sealed class CollectingSink : ILogEventSink
    {
        /// <summary>
        /// Gets the emitted events.
        /// </summary>
        public List<LogEvent> Events { get; } = [];

        /// <inheritdoc />
        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
