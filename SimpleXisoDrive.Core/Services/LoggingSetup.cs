using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace SimpleXisoDrive.Services;

/// <summary>
/// Configures the global Serilog logger with console, rolling file and bug report sinks.
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// Creates and assigns the global logger used across the application.
    /// </summary>
    public static void ConfigureLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                theme: ConsoleTheme.None)
            .WriteTo.File(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "simplexisodrive-.log"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .WriteTo.Sink(new BugReportSink(), LogEventLevel.Warning)
            .CreateLogger();
    }
}