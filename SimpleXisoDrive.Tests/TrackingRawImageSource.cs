using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// A raw image source over a byte array that records disposal.
/// </summary>
/// <param name="data">The raw image bytes served by the source.</param>
internal sealed class TrackingRawImageSource(byte[] data) : IRawImageSource
{
    /// <summary>
    /// Gets the number of <see cref="Dispose"/> calls.
    /// </summary>
    public int DisposeCount { get; private set; }

    /// <inheritdoc />
    public long Length => data.Length;

    /// <inheritdoc />
    public int Read(Span<byte> buffer, long offset)
    {
        if (buffer.IsEmpty || offset < 0 || offset >= data.Length)
        {
            return 0;
        }

        var count = (int)Math.Min(buffer.Length, data.Length - offset);
        data.AsSpan((int)offset, count).CopyTo(buffer);
        return count;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCount++;
    }
}
