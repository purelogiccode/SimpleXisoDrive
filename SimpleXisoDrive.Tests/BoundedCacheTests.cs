using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the bounded cache used by the volume implementations.
/// </summary>
public class BoundedCacheTests
{
    /// <summary>
    /// Verifies values can be stored and retrieved.
    /// </summary>
    [Fact]
    public void Set_And_TryGetValue_RoundTrip()
    {
        var cache = new BoundedCache<string, int>(4, StringComparer.OrdinalIgnoreCase);

        cache.Set("key", 42);

        Assert.True(cache.TryGetValue("KEY", out var value));
        Assert.Equal(42, value);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    /// Verifies a missing key reports failure and a default value.
    /// </summary>
    [Fact]
    public void TryGetValue_ForMissingKey_ReturnsFalse()
    {
        var cache = new BoundedCache<string, int>(4, StringComparer.OrdinalIgnoreCase);

        Assert.False(cache.TryGetValue("missing", out var value));
        Assert.Equal(0, value);
    }

    /// <summary>
    /// Verifies new entries stop being stored once the budget is exhausted.
    /// </summary>
    [Fact]
    public void Set_StopsAddingAtLimit()
    {
        var cache = new BoundedCache<string, int>(2, StringComparer.OrdinalIgnoreCase);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGetValue("a", out _));
        Assert.True(cache.TryGetValue("b", out _));
        Assert.False(cache.TryGetValue("c", out _));
    }

    /// <summary>
    /// Verifies existing entries can still be updated after the budget is exhausted.
    /// </summary>
    [Fact]
    public void Set_UpdatesExistingEntryAtLimit()
    {
        var cache = new BoundedCache<string, int>(1, StringComparer.OrdinalIgnoreCase);

        cache.Set("a", 1);
        cache.Set("a", 2);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGetValue("a", out var value));
        Assert.Equal(2, value);
    }

    /// <summary>
    /// Verifies a non-positive limit is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WithNonPositiveLimit_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedCache<string, int>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedCache<string, int>(-1));
    }

    /// <summary>
    /// Verifies the configured budget is exposed for diagnostics.
    /// </summary>
    [Fact]
    public void Limit_ReflectsConstructorArgument()
    {
        var cache = new BoundedCache<string, int>(7);

        Assert.Equal(7, cache.Limit);
    }

    /// <summary>
    /// Verifies the default comparer is ordinal (case-sensitive), unlike the volumes'
    /// case-insensitive caches.
    /// </summary>
    [Fact]
    public void Set_WithoutComparer_UsesOrdinalKeys()
    {
        var cache = new BoundedCache<string, int>(2);

        cache.Set("key", 1);

        Assert.True(cache.TryGetValue("key", out _));
        Assert.False(cache.TryGetValue("KEY", out _));
    }

    /// <summary>
    /// Verifies updating an existing key does not grow the entry count.
    /// </summary>
    [Fact]
    public void Count_AfterOverwritingExistingKey_DoesNotChange()
    {
        var cache = new BoundedCache<string, int>(4);

        cache.Set("a", 1);
        cache.Set("a", 2);
        cache.Set("b", 3);

        Assert.Equal(2, cache.Count);
    }

    /// <summary>
    /// Verifies rejected entries after the limit leave the existing entries intact.
    /// </summary>
    [Fact]
    public void Set_AfterLimit_KeepsExistingEntriesIntact()
    {
        var cache = new BoundedCache<string, int>(2);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);
        cache.Set("d", 4);

        Assert.True(cache.TryGetValue("a", out var a));
        Assert.Equal(1, a);
        Assert.True(cache.TryGetValue("b", out var b));
        Assert.Equal(2, b);
        Assert.False(cache.TryGetValue("c", out _));
        Assert.False(cache.TryGetValue("d", out _));
    }
}