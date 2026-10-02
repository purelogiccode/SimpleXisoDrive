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
    /// Asks the user whether the release page should be opened.
    /// </summary>
    /// <param name="current">The running version.</param>
    /// <param name="latest">The latest available version.</param>
    /// <param name="releaseUrl">The release page URL.</param>
    /// <returns><see langword="true"/> when the browser should be launched; otherwise <see langword="false"/>.</returns>
    public delegate bool UpdatePrompt(Version current, Version latest, string releaseUrl);

    /// <summary>
    /// Queries the latest release information and, when a newer version is available,
    /// prompts the user to open the release page. Network failures are non-fatal.
    /// </summary>
    public static Task CheckForUpdateAsync()
    {
        return CheckForUpdateAsync(Http, ConsolePrompt);
    }

    /// <summary>
    /// Queries the latest release through the supplied client, using the console prompt.
    /// Used by tests to verify request shaping and failure handling without live traffic.
    /// </summary>
    /// <param name="http">The HTTP client to send through.</param>
    internal static Task CheckForUpdateAsync(HttpClient http)
    {
        return CheckForUpdateAsync(http, ConsolePrompt);
    }

    /// <summary>
    /// Queries the latest release and notifies the user through the supplied prompt.
    /// Used by the front ends to replace the console prompt (for example with a message box).
    /// </summary>
    /// <param name="prompt">The notification/confirmation the user sees.</param>
    public static Task CheckForUpdateAsync(UpdatePrompt prompt)
    {
        return CheckForUpdateAsync(Http, prompt);
    }

    /// <summary>
    /// Queries the latest release and, when a newer version is available, notifies the
    /// user through <paramref name="prompt"/> and opens the release page on acceptance.
    /// </summary>
    /// <param name="http">The HTTP client to send through.</param>
    /// <param name="prompt">The notification/confirmation the user sees.</param>
    internal static async Task CheckForUpdateAsync(HttpClient http, UpdatePrompt prompt)
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
            if (!prompt(current, latest, htmlUrl)) return;

            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = htmlUrl,
                    UseShellExecute = true
                };
                using var process = Process.Start(psi);
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

    /// <summary>
    /// The default prompt: prints the version details to the console and reads a key.
    /// Redirected input is reported without leaving a dangling prompt.
    /// </summary>
    private static bool ConsolePrompt(Version current, Version latest, string releaseUrl)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"A newer version of {RepoName} is available:");
        Console.WriteLine($"  Current : {current}");
        Console.WriteLine($"  Latest  : {latest}");

        // Redirection means there is nobody to answer the prompt; do not leave it dangling.
        if (Console.IsInputRedirected)
        {
            Console.WriteLine($"Download it from: {releaseUrl}");
            return false;
        }

        Console.Write("Open the release page in your browser? [Y/n] ");

        var key = Console.ReadKey(true).KeyChar;
        Console.WriteLine();
        return key is not ('n' or 'N');
    }
}