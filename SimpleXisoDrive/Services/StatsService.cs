using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using Serilog;

namespace SimpleXisoDrive.Services;

/// <summary>
/// Service for reporting application launch statistics to the central stats API.
/// </summary>
public static class StatsService
{
    // Base URL for the stats API - points to the local ApplicationStats service
    private const string StatsApiBaseUrl = "https://www.purelogiccode.com";
    private const string StatsEndpoint = "/ApplicationStats/stats";

    // Application identifier for this app
    private const string ApplicationId = "simplexisodrive";

    private static readonly HttpClient Http;

    static StatsService()
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.None
            }
        };

        Http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Reports application launch statistics to the central stats API.
    /// This is a fire-and-forget operation that runs in the background and does not block the application.
    /// </summary>
    public static void ReportLaunchAsync()
    {
        // Fire and forget - don't await, don't block startup
        _ = ReportLaunchInternalAsync();
    }

    private static async Task ReportLaunchInternalAsync()
    {
        try
        {
            // Get current version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

            var request = new StatsRequest
            {
                AppId = ApplicationId,
                AppVersion = version
            };

            // Set authorization header
            Http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiKeyProvider.ApiKey);

            using var response = await Http.PostAsJsonAsync(
                $"{StatsApiBaseUrl}{StatsEndpoint}",
                request);

            if (response.IsSuccessStatusCode)
            {
                Log.Debug("Stats reported successfully.");
            }
            else
            {
                Log.Debug("Stats API returned: {StatusCode}", response.StatusCode);
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout - stats service may not be running, ignore
            Log.Debug("Stats API timeout - service may not be available.");
        }
        catch (HttpRequestException ex)
        {
            // Connection failed - stats service may not be running, ignore
            Log.Debug(ex, "Stats API unreachable");
        }
        catch (Exception ex)
        {
            // Advisory only: log locally at Debug so the bug report sink stays out of it.
            Log.Debug(ex, "Stats reporting skipped (non-fatal)");
        }
    }

    /// <summary>
    /// Request model for the stats API.
    /// </summary>
    private sealed class StatsRequest
    {
        [JsonPropertyName("applicationId")] public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("version")] public string AppVersion { get; set; } = string.Empty;
    }
}