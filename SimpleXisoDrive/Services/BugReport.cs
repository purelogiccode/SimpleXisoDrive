using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SimpleXisoDrive.Services;

/// <summary>
/// Builds bug reports from exceptions and log events, writes them to the local
/// error log, and forwards them to the remote BugReport API.
/// </summary>
public static class BugReport
{
    private const string BugReportApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    private const string ApplicationName = "SimpleXisoDrive";
    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
    private static readonly HttpClient HttpClientInstance;
    private static readonly bool IsApiLoggingConfigured;
    private static readonly Lock FileLock = new();
    private static bool _isDisposed;

    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string ErrorLogFilePath = Path.Combine(BaseDirectory, "error.log");
    private static readonly string CriticalLogFilePath = Path.Combine(BaseDirectory, "critical_error.log");

    static BugReport()
    {
        HttpClientInstance = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // API logging is configured if the key can be decrypted and the URL is present.
        IsApiLoggingConfigured = !string.IsNullOrWhiteSpace(ApiKeyProvider.ApiKey) &&
                                 !string.IsNullOrWhiteSpace(BugReportApiUrl);

        // Register for process exit to properly dispose HttpClient
        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DisposeHttpClient();
    }

    /// <summary>
    /// Disposes the static HttpClient instance. Called automatically on process exit.
    /// </summary>
    public static void DisposeHttpClient()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        HttpClientInstance.Dispose();
    }

    /// <summary>
    /// Builds a full bug report containing environment details, error details
    /// and exception details, following the standard report template.
    /// </summary>
    public static string BuildReport(string level, string errorDetails, Exception? exception)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Environment Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Application Name: {ApplicationName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Application Version: {AppVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {RuntimeInformation.OSDescription}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Architecture: OS: {RuntimeInformation.OSArchitecture}, Process: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Bitness: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")} process on {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")} OS");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Windows Version: {Environment.OSVersion.VersionString}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {BaseDirectory}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {Path.GetTempPath()}");
        sb.AppendLine();
        sb.AppendLine("=== Error Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Level: {level}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Error message: {errorDetails}");
        sb.AppendLine();
        sb.AppendLine("=== Exception Details ===");

        if (exception is null)
        {
            sb.AppendLine("Type: None");
            sb.AppendLine("Message: None");
            sb.AppendLine("Source: None");
            sb.AppendLine("StackTrace: None");
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Type: {exception.GetType().FullName}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Message: {exception.Message}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Source: {exception.Source ?? "Unknown"}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"StackTrace: {exception.StackTrace ?? "Not available"}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends a full report to the local error.log file.
    /// </summary>
    public static void WriteLocalErrorLog(string report)
    {
        try
        {
            lock (FileLock)
            {
                File.AppendAllText(ErrorLogFilePath,
                    report + Environment.NewLine + "--------------------------------------------------" +
                    Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception writeEx)
        {
            Console.Error.WriteLine($"Failed to write to local error log: {writeEx.Message}");
            WriteToCriticalLog(writeEx, $"Failed to write main error to '{ErrorLogFilePath}'.");
        }
    }

    /// <summary>
    /// Sends a bug report to the remote BugReport API.
    /// </summary>
    public static async Task SendToApiAsync(string report, string stackTrace)
    {
        if (!IsApiLoggingConfigured) return;

        try
        {
            var payload = new
            {
                message = report,
                applicationName = ApplicationName,
                version = AppVersion,
                userInfo = Environment.UserName,
                environment = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
                stackTrace
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, BugReportApiUrl);
            request.Headers.Add("X-API-KEY", ApiKeyProvider.ApiKey);
            request.Content = httpContent;

            using var response = await HttpClientInstance.SendAsync(request);

            if (response.IsSuccessStatusCode) return;

            var responseContent = await response.Content.ReadAsStringAsync();
            WriteToCriticalLog(
                new HttpRequestException(
                    $"API request failed with status code {response.StatusCode}. Response: {responseContent}"),
                "Error sending log to API.");
        }
        catch (Exception apiEx)
        {
            WriteToCriticalLog(apiEx, "Exception occurred while sending log to API.");
        }
    }

    /// <summary>
    /// Logs a crash synchronously. Used for global exception handlers where the process is terminating.
    /// Also attempts to report to the API (fire-and-forget since this is a sync method).
    /// </summary>
    public static void LogFatalException(Exception ex, string contextMessage)
    {
        try
        {
            var report = BuildReport("Fatal", contextMessage, ex);

            Console.Error.WriteLine("\n--- CRITICAL CRASH ---");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine($"Details written to: {ErrorLogFilePath}");

            WriteLocalErrorLog(report);

            // Report to API (fire-and-forget since this is a sync method)
            if (IsApiLoggingConfigured)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendToApiAsync(report, ex.ToString());
                    }
                    catch
                    {
                        // Ignore API errors in fatal handler
                    }
                });
            }
        }
        catch (Exception writeEx)
        {
            // Last ditch effort
            WriteToCriticalLog(writeEx, $"LogFatalException failed. Original: {ex.Message}");
        }
    }

    private static void WriteToCriticalLog(Exception ex, string contextMessage)
    {
        try
        {
            var criticalContent = new StringBuilder();
            criticalContent.AppendLine("--- CRITICAL LOGGING ERROR ---");
            criticalContent.AppendLine(CultureInfo.InvariantCulture,
                $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
            criticalContent.AppendLine(CultureInfo.InvariantCulture, $"Application: {ApplicationName}");
            criticalContent.AppendLine(CultureInfo.InvariantCulture, $"Version: {AppVersion}");
            criticalContent.AppendLine(CultureInfo.InvariantCulture, $"Context: {contextMessage}");
            criticalContent.AppendLine(CultureInfo.InvariantCulture, $"Exception Type: {ex.GetType().Name}");
            criticalContent.AppendLine(CultureInfo.InvariantCulture, $"Exception Message: {ex.Message}");
            criticalContent.AppendLine(CultureInfo.InvariantCulture, $"Stack Trace:\n{ex.StackTrace}");
            criticalContent.AppendLine("--------------------------------------------------\n");

            File.AppendAllText(CriticalLogFilePath, criticalContent.ToString(), Encoding.UTF8);
        }
        catch (Exception writeEx)
        {
            Console.Error.WriteLine(
                $"FATAL: Could not write to critical error log '{CriticalLogFilePath}'. Reason: {writeEx.Message}");
            Console.Error.WriteLine($"Original critical error: {contextMessage} - {ex.Message}");
        }
    }
}
