using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// A concurrent cache with a fixed entry budget. Once the budget is reached new
/// entries are not stored (existing entries can still be updated), so a long-lived
/// mount over a large tree cannot grow the cache without bound.
/// </summary>
/// <typeparam name="TKey">The cache key type.</typeparam>
/// <typeparam name="TValue">The cached value type.</typeparam>
internal sealed class BoundedCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedCache{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="limit">The maximum number of entries to store.</param>
    /// <param name="comparer">The key comparer, or <see langword="null"/> for the default equality comparer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is not positive.</exception>
    public BoundedCache(int limit, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        Limit = limit;
        _entries = new ConcurrentDictionary<TKey, TValue>(comparer);
    }

    /// <summary>
    /// Gets the number of stored entries.
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Gets the entry budget.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Attempts to retrieve a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">When this method returns, the cached value if present.</param>
    /// <returns><see langword="true"/> when the key is cached; otherwise <see langword="false"/>.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return _entries.TryGetValue(key, out value);
    }

    /// <summary>
    /// Stores a value unless the budget is exhausted; existing keys are always updated.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to store.</param>
    public void Set(TKey key, TValue value)
    {
        if (_entries.ContainsKey(key) || _entries.Count < Limit)
        {
            _entries[key] = value;
        }
    }
}