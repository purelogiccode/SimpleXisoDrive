namespace SimpleXisoDrive;

/// <summary>
/// Thrown when the command line is invalid.
/// </summary>
/// <param name="message">The user-facing error message.</param>
/// <param name="showUsage">Whether the usage text should be printed alongside the error.</param>
internal sealed class CommandLineException(string message, bool showUsage) : Exception(message)
{
    /// <summary>
    /// Gets a value indicating whether the usage text should be shown.
    /// </summary>
    public bool ShowUsage { get; } = showUsage;
}
