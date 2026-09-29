using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using Serilog;
using SimpleXisoDrive.Core.Models;

namespace SimpleXisoDrive.Core.Services;

/// <summary>
/// Service for reporting application launch statistics to the central stats API.
/// </summary>
public static class StatsService
{
    // Base URL for the stats API - points to the ApplicationStats service
    private const string StatsApiBaseUrl = "https://www.purelogiccode.com";
    private const string StatsEndpoint = "/ApplicationStats/stats";

    // Application identifier for this app
    private const string ApplicationId = "simplexisodrive";

    private static readonly HttpClient Http;

    static StatsService()
    {
        Http = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Reports application launch statistics to the central stats API.
    /// This is a fire-and-forget operation that runs in the background and does not block the application.
    /// </summary>
    public static void ReportLaunch()
    {
        try
        {
            // Fire and forget - don't await, don't block startup
            _ = ReportLaunchAsync(Http);
        }
        catch (Exception ex)
        {
            // Advisory only: log locally at Debug so the bug report sink stays out of it.
            Log.Debug(ex, "Stats reporting could not be started (non-fatal)");
        }
    }

    /// <summary>
    /// Reports launch statistics through the supplied client. Used by tests to verify
    /// request shaping without live traffic.
    /// </summary>
    /// <param name="http">The HTTP client to send through.</param>
    internal static async Task ReportLaunchAsync(HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyProvider.ApiKey))
        {
            // Without a usable key the API would reject the request with 401.
            Log.Debug("Stats reporting skipped: API key unavailable.");
            return;
        }

        try
        {
            // Get current version from the entry assembly (the Core assembly when the
            // service is used from tests or tooling).
            var version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
                .GetName().Version?.ToString() ?? "0.0.0";

            var request = new StatsRequest
            {
                ApplicationId = ApplicationId,
                Version = version
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{StatsApiBaseUrl}{StatsEndpoint}");
            httpRequest.Content = JsonContent.Create(request);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiKeyProvider.ApiKey);

            using var response = await http.SendAsync(httpRequest).ConfigureAwait(false);

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
}