using Serilog;

namespace SimpleXisoDrive.Core;

/// <summary>
/// Provides a single shared console key press for interactive waits. The
/// drag-and-drop unmount watcher and an error prompt then await the same key
/// press instead of two blocking reads stealing each other's key.
/// </summary>
internal static class ConsoleKeyPress
{
    private static TaskCompletionSource<ConsoleKeyInfo> _pressed = CreateSource();

    private static int _readerStarted;

    /// <summary>
    /// Gets a task that completes when a key is pressed. The reader starts on first
    /// use; when the console cannot read a key (for example redirected input) the
    /// task completes immediately.
    /// </summary>
    /// <returns>A task that completes on the next key press.</returns>
    public static Task<ConsoleKeyInfo> WaitAsync()
    {
        // Capture the source so a concurrent Reset can never leave the caller awaiting a
        // task that no reader will complete.
        var source = Volatile.Read(ref _pressed);

        if (Interlocked.Exchange(ref _readerStarted, 1) == 0)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    source.TrySetResult(Console.ReadKey(true));
                }
                catch (Exception ex)
                {
                    // Redirected input (or no console): complete immediately.
                    Log.Debug(ex, "Console key read unavailable; completing immediately");
                    source.TrySetResult(default);
                }
            });
        }

        return source.Task;
    }

    /// <summary>
    /// Resets the shared wait so a future key press can be awaited again (for example
    /// after a clean unmount). Only safe when no caller is still blocked on the
    /// previous wait.
    /// </summary>
    internal static void Reset()
    {
        Volatile.Write(ref _pressed, CreateSource());
        Interlocked.Exchange(ref _readerStarted, 0);
    }

    private static TaskCompletionSource<ConsoleKeyInfo> CreateSource()
    {
        return new TaskCompletionSource<ConsoleKeyInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}