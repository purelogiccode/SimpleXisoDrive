using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace SimpleXisoDrive.Core.Services;

/// <summary>
/// Configures the global Serilog logger with console, rolling file and bug report sinks.
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// Controls the minimum level written to the console. Verbose (debug) output is
    /// enabled by the front ends when the user passes <c>--debug</c>; the file sink
    /// always keeps the full debug log for diagnostics.
    /// </summary>
    public static LoggingLevelSwitch ConsoleLevelSwitch { get; } = new(LogEventLevel.Information);

    /// <summary>
    /// Creates and assigns the global logger used across the application.
    /// </summary>
    public static void ConfigureLogger()
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    levelSwitch: ConsoleLevelSwitch,
                    theme: ConsoleTheme.None)
                .WriteTo.File(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "simplexisodrive-.log"),
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .WriteTo.Sink(new BugReportSink(), LogEventLevel.Warning)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            // Serilog itself is unavailable: report on the console so the failure is
            // visible, then rethrow for the caller to decide (the app keeps running).
            Console.Error.WriteLine($"Failed to configure logging: {ex.Message}");
            throw;
        }
    }
}