namespace SimpleXisoDrive;

/// <summary>
/// Provides a single shared console key press for interactive waits. The
/// drag-and-drop unmount watcher and an error prompt then await the same key
/// press instead of two blocking reads stealing each other's key.
/// </summary>
internal static class ConsoleKeyPress
{
    private static readonly TaskCompletionSource<ConsoleKeyInfo> Pressed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static int _readerStarted;

    /// <summary>
    /// Gets a task that completes when a key is pressed. The reader starts on first
    /// use; when the console cannot read a key (for example redirected input) the
    /// task completes immediately.
    /// </summary>
    /// <returns>A task that completes on the next key press.</returns>
    public static Task<ConsoleKeyInfo> WaitAsync()
    {
        if (Interlocked.Exchange(ref _readerStarted, 1) == 0)
        {
            _ = Task.Run(static () =>
            {
                try
                {
                    Pressed.TrySetResult(Console.ReadKey(true));
                }
                catch
                {
                    Pressed.TrySetResult(default);
                }
            });
        }

        return Pressed.Task;
    }
}
