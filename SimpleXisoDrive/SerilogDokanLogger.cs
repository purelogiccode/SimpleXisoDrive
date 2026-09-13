using Serilog;
using Serilog.Events;

namespace SimpleXisoDrive;

/// <summary>
/// Adapter that routes DokanNet's internal logging into Serilog.
/// </summary>
public sealed class SerilogDokanLogger : DokanNet.Logging.ILogger
{
    /// <summary>
    /// Gets a value indicating whether debug-level logging is enabled.
    /// </summary>
    public bool DebugEnabled => Log.IsEnabled(LogEventLevel.Debug);

    /// <summary>
    /// Writes a debug-level message to the Serilog logger.
    /// </summary>
    /// <param name="message">The message template to log.</param>
    /// <param name="args">The values to substitute into the message template.</param>
    public void Debug(string message, params object[] args) => Log.Debug(message, args);

    /// <summary>
    /// Writes an information-level message to the Serilog logger.
    /// </summary>
    /// <param name="message">The message template to log.</param>
    /// <param name="args">The values to substitute into the message template.</param>
    public void Info(string message, params object[] args) => Log.Information(message, args);

    /// <summary>
    /// Writes a warning-level message to the Serilog logger.
    /// </summary>
    /// <param name="message">The message template to log.</param>
    /// <param name="args">The values to substitute into the message template.</param>
    public void Warn(string message, params object[] args) => Log.Warning(message, args);

    /// <summary>
    /// Writes an error-level message to the Serilog logger.
    /// </summary>
    /// <param name="message">The message template to log.</param>
    /// <param name="args">The values to substitute into the message template.</param>
    public void Error(string message, params object[] args) => Log.Error(message, args);

    /// <summary>
    /// Writes a fatal-level message to the Serilog logger.
    /// </summary>
    /// <param name="message">The message template to log.</param>
    /// <param name="args">The values to substitute into the message template.</param>
    public void Fatal(string message, params object[] args) => Log.Fatal(message, args);
}
