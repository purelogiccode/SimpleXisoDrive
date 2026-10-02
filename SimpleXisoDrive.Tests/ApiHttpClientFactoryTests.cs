using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the shared API HTTP client factory: timeout shaping and client isolation.
/// </summary>
public class ApiHttpClientFactoryTests
{
    /// <summary>
    /// Verifies the requested timeout is applied to the client.
    /// </summary>
    [Fact]
    public void Create_SetsRequestedTimeout()
    {
        using var client = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    /// <summary>
    /// Verifies each call returns an independent client so disposing one does not
    /// affect the others (the shared handler is never disposed).
    /// </summary>
    [Fact]
    public void Create_ReturnsDistinctClients()
    {
        using var first = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(5));
        using var second = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(10));

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// Verifies an infinite timeout is accepted for long-running requests.
    /// </summary>
    [Fact]
    public void Create_WithInfiniteTimeout_IsAllowed()
    {
        using var client = ApiHttpClientFactory.Create(Timeout.InfiniteTimeSpan);

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    /// <summary>
    /// Verifies the factory does not inject default request headers; each service adds its own.
    /// </summary>
    [Fact]
    public void Create_HasNoDefaultRequestHeaders()
    {
        using var client = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(5));

        Assert.Empty(client.DefaultRequestHeaders);
    }

    /// <summary>
    /// Verifies disposing one client does not break the creation of the next one over
    /// the shared (never-disposed) handler.
    /// </summary>
    [Fact]
    public void Create_AfterDisposingAClient_StillWorks()
    {
        var first = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(5));
        first.Dispose();

        using var second = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(7));
        Assert.Equal(TimeSpan.FromSeconds(7), second.Timeout);
    }
}
