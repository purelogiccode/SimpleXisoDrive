using Serilog;

namespace SimpleXisoDrive;

/// <summary>
/// Selects a free drive letter for automatic (drag-and-drop) mounts.
/// </summary>
internal static class DriveLetterSelector
{
    /// <summary>
    /// The preferred drive letters, in order.
    /// </summary>
    internal static readonly char[] PreferredLetters = ['M', 'N', 'O', 'P', 'Q', 'R'];

    /// <summary>
    /// Finds the first preferred drive letter that is not currently in use.
    /// </summary>
    /// <returns>A mount path such as <c>M:\</c>, or <see langword="null"/> when none is free.</returns>
    public static string? FindAvailableDriveLetter()
    {
        try
        {
            var usedLetters = DriveInfo.GetDrives()
                .Select(static d => d.Name)
                .Where(static name => name.Length > 0)
                .Select(static name => name[0])
                .ToHashSet();

            foreach (var letter in PreferredLetters)
            {
                if (!usedLetters.Contains(letter))
                {
                    var drivePath = $"{letter}:\\";
                    Log.Debug("Found available drive letter: {DrivePath}", drivePath);
                    return drivePath;
                }
            }

            // Exhausting the preferred range is a user environment condition; the
            // caller prints a friendly error and returns a non-zero exit code.
            Log.Information("No available drive letters found in preferred range M-R");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking drive letters");
            return null;
        }
    }
}