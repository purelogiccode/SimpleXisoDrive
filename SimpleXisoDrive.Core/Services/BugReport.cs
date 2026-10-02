using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SimpleXisoDrive.Core.Models;

namespace SimpleXisoDrive.Core.Services;

/// <summary>
/// Builds bug reports from exceptions and log events, writes them to the local
/// error log, and forwards them to the remote BugReport API.
/// </summary>
public static class BugReport
{
    private const string BugReportApiUrl = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    private const string ApplicationName = "SimpleXisoDrive";

    private static readonly string AppVersion =
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString() ?? "Unknown";

    private static readonly HttpClient HttpClientInstance;
    private static readonly bool IsApiLoggingConfigured;
    private static readonly Lock FileLock = new();
    private static bool _isDisposed;
    private static int _pendingReports;

    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// Gets the path of the local error log.
    /// </summary>
    internal static string ErrorLogFilePath { get; private set; } = Path.Combine(BaseDirectory, "error.log");

    /// <summary>
    /// Gets the path of the critical logging-error log.
    /// </summary>
    internal static string CriticalLogFilePath { get; private set; } =
        Path.Combine(BaseDirectory, "critical_error.log");

    /// <summary>
    /// Overrides the local log file paths so tests never append to the real logs. Pass
    /// <see langword="null"/> to restore the default location next to the application.
    /// </summary>
    /// <param name="errorLogPath">The error log path, or <see langword="null"/> for the default.</param>
    /// <param name="criticalLogPath">The critical log path, or <see langword="null"/> for the default.</param>
    internal static void OverrideLogFilePaths(string? errorLogPath, string? criticalLogPath)
    {
        ErrorLogFilePath = errorLogPath ?? Path.Combine(BaseDirectory, "error.log");
        CriticalLogFilePath = criticalLogPath ?? Path.Combine(BaseDirectory, "critical_error.log");
    }

    static BugReport()
    {
        HttpClientInstance = ApiHttpClientFactory.Create(TimeSpan.FromSeconds(30));

        // API logging is configured if the key can be decrypted.
        IsApiLoggingConfigured = !string.IsNullOrWhiteSpace(ApiKeyProvider.ApiKey);

        // Register for process exit to properly dispose HttpClient
        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DisposeHttpClient();
    }

    /// <summary>
    /// Disposes the static HttpClient instance. Called automatically on process exit.
    /// </summary>
    private static void DisposeHttpClient()
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
        try
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
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{OperatingSystemDisplayName()} Version: {Environment.OSVersion.VersionString}");
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
        catch (Exception ex)
        {
            // Never let report construction break the logging pipeline: the message is
            // written directly to the console so a broken report cannot re-enter Serilog.
            Console.Error.WriteLine($"Failed to build bug report: {ex.Message}");
            return $"=== Error Details ==={Environment.NewLine}Error message: {errorDetails}{Environment.NewLine}" +
                   $"Report construction failed: {ex.Message}";
        }
    }

    private static string OperatingSystemDisplayName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        return OperatingSystem.IsMacOS() ? "macOS" : "OperatingSystem";
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
    /// <param name="report">The rendered report body.</param>
    /// <param name="stackTrace">The stack trace to attach, or a placeholder.</param>
    public static Task SendToApiAsync(string report, string stackTrace)
    {
        return SendToApiAsync(report, stackTrace, HttpClientInstance);
    }

    /// <summary>
    /// Sends a bug report through the supplied client. Used by tests to verify request
    /// shaping and failure handling without live traffic.
    /// </summary>
    /// <param name="report">The rendered report body.</param>
    /// <param name="stackTrace">The stack trace to attach, or a placeholder.</param>
    /// <param name="http">The HTTP client to send through.</param>
    internal static async Task SendToApiAsync(string report, string stackTrace, HttpClient http)
    {
        if (!IsApiLoggingConfigured) return;

        try
        {
            var payload = new BugReportRequest
            {
                Message = report,
                ApplicationName = ApplicationName,
                Version = AppVersion,
                UserInfo = Environment.UserName,
                Environment = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
                StackTrace = stackTrace
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, BugReportApiUrl);
            request.Headers.Add("X-API-KEY", ApiKeyProvider.ApiKey);
            request.Content = httpContent;

            using var response = await http.SendAsync(request).ConfigureAwait(false);

            if (response.IsSuccessStatusCode) return;

            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
    /// Sends a report while counting it as pending, so shutdown can wait for in-flight
    /// reports before the process exits. Never throws.
    /// </summary>
    /// <param name="report">The rendered report body.</param>
    /// <param name="stackTrace">The stack trace to attach, or a placeholder.</param>
    internal static async Task SendTrackedAsync(string report, string stackTrace)
    {
        Interlocked.Increment(ref _pendingReports);
        try
        {
            await SendToApiAsync(report, stackTrace, HttpClientInstance).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingReports);
        }
    }

    /// <summary>
    /// Gets the number of bug reports currently in flight.
    /// </summary>
    internal static int PendingReports => Volatile.Read(ref _pendingReports);

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for in-flight remote bug reports to finish
    /// so a clean shutdown does not cut them off.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    public static async Task WaitForPendingReportsAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (Volatile.Read(ref _pendingReports) > 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(false);
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

            // Report to API (tracked so shutdown can wait for it; never throws)
            if (IsApiLoggingConfigured)
            {
                _ = SendTrackedAsync(report, ex.ToString());
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

            lock (FileLock)
            {
                File.AppendAllText(CriticalLogFilePath, criticalContent.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception writeEx)
        {
            Console.Error.WriteLine(
                $"FATAL: Could not write to critical error log '{CriticalLogFilePath}'. Reason: {writeEx.Message}");
            Console.Error.WriteLine($"Original critical error: {contextMessage} - {ex.Message}");
        }
    }
}