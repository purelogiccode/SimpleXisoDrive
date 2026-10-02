using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Dokan installation path helpers.
/// </summary>
[Collection(GlobalLoggerCollection.Name)]
public class DokanInstallationTests
{
    /// <summary>
    /// Verifies that a missing Dokan runtime (an expected user-setup condition) is not
    /// logged at Warning level or higher, so it is not forwarded to the bug report API.
    /// On a machine that already has Dokan installed this passes trivially.
    /// </summary>
    [Fact]
    public void Check_DoesNotLogWarningOrHigher()
    {
        var original = Log.Logger;
        var sink = new CollectingSink();
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            var originalError = Console.Error;
            using var suppressed = new StringWriter();
            Console.SetError(suppressed);

            try
            {
                _ = DokanInstallation.Check();
            }
            finally
            {
                Console.SetError(originalError);
            }

            // Filter by template because tests run in parallel and share the global logger.
            Assert.DoesNotContain(sink.Events, e =>
                e.Level >= LogEventLevel.Warning &&
                (e.MessageTemplate.Text.StartsWith("Dokan check FAILED", StringComparison.Ordinal) ||
                 e.MessageTemplate.Text.StartsWith("Dokan driver warning", StringComparison.Ordinal)));
        }
        finally
        {
            Log.Logger = original;
        }
    }

    /// <summary>
    /// Verifies the library path points at dokan2.dll under the system directory.
    /// </summary>
    [Fact]
    public void LibraryPath_PointsAtSystem32Dokan2Dll()
    {
        Assert.True(Path.IsPathRooted(DokanInstallation.LibraryPath));
        Assert.Equal("dokan2.dll", Path.GetFileName(DokanInstallation.LibraryPath));
        Assert.StartsWith(Environment.SystemDirectory, DokanInstallation.LibraryPath,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the driver path points at dokan2.sys under the drivers directory.
    /// </summary>
    [Fact]
    public void DriverPath_PointsAtSystem32DriversDokan2Sys()
    {
        Assert.True(Path.IsPathRooted(DokanInstallation.DriverPath));
        Assert.Equal("dokan2.sys", Path.GetFileName(DokanInstallation.DriverPath));
        Assert.EndsWith(Path.Combine("drivers", "dokan2.sys"), DokanInstallation.DriverPath,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A thread-safe Serilog sink that records the emitted events for assertions.
    /// </summary>
    private sealed class CollectingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        /// <summary>
        /// Gets a snapshot of the emitted events.
        /// </summary>
        public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

        /// <inheritdoc />
        public void Emit(LogEvent logEvent)
        {
            _events.Enqueue(logEvent);
        }
    }
}