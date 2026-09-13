using Serilog;
using Serilog.Events;

namespace SimpleXisoDrive;

/// <summary>
/// Adapter that routes DokanNet's internal logging into Serilog.
/// </summary>
public sealed class SerilogDokanLogger : DokanNet.Logging.ILogger
{
    public bool DebugEnabled => Log.IsEnabled(LogEventLevel.Debug);

    public void Debug(string message, params object[] args) => Log.Debug(message, args);

    public void Info(string message, params object[] args) => Log.Information(message, args);

    public void Warn(string message, params object[] args) => Log.Warning(message, args);

    public void Error(string message, params object[] args) => Log.Error(message, args);

    public void Fatal(string message, params object[] args) => Log.Fatal(message, args);
}

