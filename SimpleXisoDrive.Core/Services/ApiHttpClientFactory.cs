using System.Net.Security;
using System.Security.Authentication;

namespace SimpleXisoDrive.Core.Services;

/// <summary>
/// Creates the HTTP clients used for the API calls (bug reports, launch statistics and
/// update checks) from one shared, consistently configured connection pool.
/// </summary>
internal static class ApiHttpClientFactory
{
    /// <summary>
    /// The handler shared by every API client. It is intentionally never disposed so
    /// disposing an individual client cannot tear down the shared connection pool.
    /// </summary>
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            // Let the OS negotiate the best protocol instead of pinning versions.
            EnabledSslProtocols = SslProtocols.None
        }
    };

    /// <summary>
    /// Creates a client over the shared handler with the specified per-request timeout.
    /// </summary>
    /// <param name="timeout">The client timeout.</param>
    /// <returns>A configured HTTP client.</returns>
    public static HttpClient Create(TimeSpan timeout)
    {
        return new HttpClient(SharedHandler, disposeHandler: false)
        {
            Timeout = timeout
        };
    }
}