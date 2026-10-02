using SimpleXisoDrive.Core;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the shared console key-press wait used by the interactive prompts.
/// </summary>
public class ConsoleKeyPressTests
{
    /// <summary>
    /// Verifies the wait completes immediately with a default key when input is redirected
    /// (which is the case in the test host), instead of blocking forever.
    /// </summary>
    [Fact]
    public async Task WaitAsync_WithRedirectedInput_CompletesWithDefaultKey()
    {
        var originalInput = Console.In;
        Console.SetIn(new StringReader(string.Empty));

        try
        {
            var task = ConsoleKeyPress.WaitAsync();
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.Same(task, completed);
            Assert.Equal(default, await task);
        }
        finally
        {
            Console.SetIn(originalInput);
        }
    }

    /// <summary>
    /// Verifies a reset starts a fresh wait so future prompts work after the first key press.
    /// </summary>
    [Fact]
    public async Task Reset_StartsFreshWait_ThatStillCompletesWithRedirectedInput()
    {
        await ConsoleKeyPress.WaitAsync();

        ConsoleKeyPress.Reset();

        var task = ConsoleKeyPress.WaitAsync();
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(task, completed);
        Assert.Equal(default, await task);
    }

    /// <summary>
    /// Verifies concurrent callers share one wait instead of starting competing reads.
    /// </summary>
    [Fact]
    public async Task WaitAsync_CalledTwice_ReturnsTheSameTask()
    {
        ConsoleKeyPress.Reset();

        var first = ConsoleKeyPress.WaitAsync();
        var second = ConsoleKeyPress.WaitAsync();

        Assert.Same(first, second);
        await Task.WhenAll(first, second);
    }
}