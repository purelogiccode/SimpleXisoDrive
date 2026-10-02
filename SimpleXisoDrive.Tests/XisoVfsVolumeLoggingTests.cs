using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests that expected lookup outcomes (missing paths probed by Windows) are not
/// logged at Warning level or higher, so they are not forwarded to the bug report API.
/// </summary>
[Collection(GlobalLoggerCollection.Name)]
public class XisoVfsVolumeLoggingTests
{
    private static string WriteTempImage(byte[] image)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(path, image);
        return path;
    }

    /// <summary>
    /// Verifies a missing nested path on a path-based volume does not produce a warning or error.
    /// </summary>
    [Fact]
    public void GetEntry_ForMissingNestedPath_OnPathVolume_DoesNotLogWarningOrHigher()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);
            var (entry, events) = CaptureLog(() =>
                volume.GetEntry(@"\System Volume Information\MountPointManagerRemoteDatabase"));

            Assert.Null(entry);
            AssertNoLookupError(events);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies missing directories on a path-based volume do not produce a warning or error.
    /// </summary>
    [Fact]
    public void GetFolderList_ForMissingDirectory_OnPathVolume_DoesNotLogWarningOrHigher()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);
            var (children, events) = CaptureLog(() => volume.GetFolderList(@"\Images\Camera").ToList());

            Assert.Empty(children);
            AssertNoLookupError(events);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies missing paths on a stream-based volume do not produce a warning or error.
    /// </summary>
    [Fact]
    public void GetEntry_ForMissingPath_OnStreamVolume_DoesNotLogWarningOrHigher()
    {
        using var stream = new MemoryStream(TestImageFactory.CreateMinimalXdvdfsImage());
        using var volume = new XisoVfsVolume(stream, "embedded.iso");
        var (entry, events) = CaptureLog(() => volume.GetEntry(@"\Images\Camera"));

        Assert.Null(entry);
        AssertNoLookupError(events);
    }

    /// <summary>
    /// Asserts that no lookup failure was reported at Warning level or higher. The events are
    /// filtered by template because tests run in parallel and share the global logger: other
    /// tests may emit unrelated events while this sink is attached.
    /// </summary>
    private static void AssertNoLookupError(IReadOnlyCollection<LogEvent> events)
    {
        Assert.DoesNotContain(events, e =>
            e.Level >= LogEventLevel.Warning &&
            (e.MessageTemplate.Text.StartsWith("GetEntry failed", StringComparison.Ordinal) ||
             e.MessageTemplate.Text.StartsWith("GetFolderList failed", StringComparison.Ordinal)));
    }

    private static (T Result, IReadOnlyCollection<LogEvent> Events) CaptureLog<T>(Func<T> action)
    {
        var original = Log.Logger;
        var sink = new CollectingSink();
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        try
        {
            return (action(), sink.Events);
        }
        finally
        {
            Log.Logger = original;
        }
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
