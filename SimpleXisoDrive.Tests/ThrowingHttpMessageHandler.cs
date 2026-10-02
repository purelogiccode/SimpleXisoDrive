namespace SimpleXisoDrive.Tests;

/// <summary>
/// An <see cref="HttpMessageHandler"/> stub whose send always fails with the supplied
/// exception, used to exercise the network-failure handling of the API services.
/// </summary>
/// <param name="exception">The exception to surface from <see cref="SendAsync"/>.</param>
internal sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    private readonly Exception _exception = exception;

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromException<HttpResponseMessage>(_exception);
    }
}
