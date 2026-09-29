using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace SimpleXisoDrive.Core.Services;

/// <summary>
/// Checks the GitHub releases API for a newer version of the application and
/// offers to open the release page in the default browser.
/// </summary>
public static class UpdateChecker
{
    private const string RepoOwner = "purelogiccode";
    private const string RepoName = "SimpleXisoDrive";
    private const string LatestApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex VersionRegex = new(@"\d+\.\d+\.\d+", RegexOptions.Compiled, RegexMatchTimeout);

    private static readonly HttpClient Http;

    static UpdateChecker()
    {
        Http = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Queries the latest release information and, when a newer version is available,
    /// prompts the user to open the release page. Network failures are non-fatal.
    /// </summary>
    public static Task CheckForUpdateAsync()
    {
        return CheckForUpdateAsync(Http);
    }

    /// <summary>
    /// Queries the latest release through the supplied client. Used by tests to verify
    /// request shaping and failure handling without live traffic.
    /// </summary>
    /// <param name="http">The HTTP client to send through.</param>
    internal static async Task CheckForUpdateAsync(HttpClient http)
    {
        try
        {
            if (!http.DefaultRequestHeaders.Contains("User-Agent"))
                http.DefaultRequestHeaders.Add("User-Agent", $"{RepoName}-UpdateChecker");

            using var resp = await http.GetAsync(LatestApiUrl).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return;

            await using var jsonStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(jsonStream).ConfigureAwait(false);

            var tagName = doc.RootElement.GetProperty("tag_name").GetString();
            var htmlUrl = doc.RootElement.GetProperty("html_url").GetString();
            if (tagName is null || htmlUrl is null) return;

            var m = VersionRegex.Match(tagName);
            if (!m.Success) return;

            var latest = Version.Parse(m.Value);
            // Use the entry assembly like BugReport and StatsService so the comparison
            // always uses the front end's version, not the Core assembly's.
            var current = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version
                          ?? new Version(0, 0, 0, 0);

            if (latest <= current) return;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"A newer version of {RepoName} is available:");
            Console.WriteLine($"  Current : {current}");
            Console.WriteLine($"  Latest  : {latest}");

            // Redirection means there is nobody to answer the prompt; do not leave it dangling.
            if (Console.IsInputRedirected)
            {
                Console.WriteLine($"Download it from: {htmlUrl}");
                return;
            }

            Console.Write("Open the release page in your browser? [Y/n] ");

            var key = Console.ReadKey(true).KeyChar;
            Console.WriteLine();

            if (key is 'n' or 'N')
                return;

            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = htmlUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Console.WriteLine("Browser opened to latest release page.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not launch browser automatically: {ex.Message}");
                Console.WriteLine($"You can open the page manually: {htmlUrl}");
            }
        }
        catch (Exception ex)
        {
            // Non-fatal: transient network issues (e.g. slow connections) are expected;
            // logged locally only, deliberately NOT forwarded to the bug report API.
            Log.Information(ex, "Update check skipped (non-fatal)");
        }
    }
}