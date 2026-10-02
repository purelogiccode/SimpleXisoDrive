namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// Classifies exceptions raised by XISOSharp lookups that mean "the path does not
/// exist in the image" rather than an application failure. Windows routinely probes
/// paths that are absent (for example <c>\System Volume Information</c>), so these
/// outcomes must stay below the bug-report threshold.
/// </summary>
/// <remarks>
/// XISOSharp signals a missing path with an <see cref="InvalidDataException"/> whose
/// message starts with <c>"Path not found"</c>. The match lives here so the string is
/// pinned by a single test and a library message change only has to be handled once.
/// </remarks>
internal static class XisoPathNotFound
{
    private const string MissingPathMessage = "Path not found";

    /// <summary>
    /// Determines whether an exception (or one of its inner exceptions) represents a
    /// path that simply does not exist in the image.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> when the path is missing from the image.</returns>
    public static bool Is(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return true;
            }

            if (ex is InvalidDataException &&
                ex.Message.StartsWith(MissingPathMessage, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
