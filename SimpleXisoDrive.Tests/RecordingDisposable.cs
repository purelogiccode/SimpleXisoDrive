namespace SimpleXisoDrive.Tests;

/// <summary>
/// An <see cref="IDisposable"/> that records disposal, optionally throwing.
/// </summary>
/// <param name="throwOnDispose">Whether <see cref="Dispose"/> throws after recording the call.</param>
internal sealed class RecordingDisposable(bool throwOnDispose = false) : IDisposable
{
    private readonly bool _throwOnDispose = throwOnDispose;

    /// <summary>
    /// Gets the number of <see cref="Dispose"/> calls.
    /// </summary>
    public int DisposeCount { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCount++;

        if (_throwOnDispose)
        {
            throw new InvalidOperationException("owner dispose failure");
        }
    }
}
