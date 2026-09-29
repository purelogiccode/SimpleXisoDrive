using System.Net;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// An <see cref="HttpMessageHandler"/> stub that records outbound requests and returns a
/// canned response, so HTTP services can be tested without live traffic.
/// </summary>
internal sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode = statusCode;
    private readonly string _responseBody = responseBody;

    /// <summary>
    /// Gets the requests that were sent, in order. Method, URI and headers remain readable
    /// after the caller disposes the request; request bodies are captured in
    /// <see cref="RequestBodies"/> because disposal clears the content.
    /// </summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Gets the request bodies observed by the handler.
    /// </summary>
    public List<string> RequestBodies { get; } = [];

    /// <summary>
    /// Gets the raw <c>User-Agent</c> header value of each request.
    /// </summary>
    public List<string> UserAgents { get; } = [];

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));
        UserAgents.Add(request.Headers.UserAgent.ToString());

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };
    }
}