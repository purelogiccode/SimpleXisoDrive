using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace SimpleXisoDrive.Services;

public static class LoggingSetup
{
    public static void ConfigureLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                theme: ConsoleTheme.None,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "simplexisodrive-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(new BugReportSink(), LogEventLevel.Warning)
            .CreateLogger();
    }
}
