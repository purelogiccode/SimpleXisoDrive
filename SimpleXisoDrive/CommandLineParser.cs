using SimpleXisoDrive.Models;

namespace SimpleXisoDrive;

/// <summary>
/// Parses and validates the Windows command line. Extracted from the entry point so the
/// argument rules can be unit tested.
/// </summary>
internal static class CommandLineParser
{
    /// <summary>
    /// Parses the supplied arguments. At least one argument is required; the zero-argument
    /// case (usage display) is handled by the caller. Options may appear before or after
    /// the mount path, matching the Unix front end.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The validated arguments.</returns>
    /// <exception cref="CommandLineException">Thrown when an argument is invalid.</exception>
    public static CommandLineArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var imagePath = ValidateImagePath(args[0]);
        var debug = false;
        var launch = false;
        var imageIso = false;
        string? mountPath = null;

        foreach (var argument in args.Skip(1))
        {
            if (MatchesAny(argument, "-d", "--debug"))
            {
                debug = true;
            }
            else if (MatchesAny(argument, "-l", "--launch"))
            {
                launch = true;
            }
            else if (MatchesAny(argument, "-i", "--image-iso"))
            {
                imageIso = true;
            }
            else if (argument.StartsWith('-'))
            {
                throw new CommandLineException($"unknown option '{argument}'.", showUsage: true);
            }
            else if (mountPath is null)
            {
                mountPath = argument;
            }
            else
            {
                throw new CommandLineException($"unexpected argument '{argument}'.", showUsage: false);
            }
        }

        // Without a mount path the caller falls back to drag-and-drop mode, which
        // selects a free drive letter and opens Explorer after mounting.
        var isDragAndDrop = mountPath is null;

        return new CommandLineArguments
        {
            ImagePath = imagePath,
            MountPath = mountPath,
            IsDragAndDrop = isDragAndDrop,
            Debug = debug,
            Launch = launch || isDragAndDrop,
            ImageIso = imageIso
        };
    }

    /// <summary>
    /// Determines whether an argument requests the usage text. Matching is case-insensitive
    /// and consistent with the Unix front end.
    /// </summary>
    /// <param name="argument">The command-line argument to test.</param>
    /// <returns><see langword="true"/> when the argument is <c>-h</c> or <c>--help</c>.</returns>
    public static bool IsHelpOption(string argument)
    {
        return MatchesAny(argument, "-h", "--help");
    }

    private static string ValidateImagePath(string isoPath)
    {
        if (string.IsNullOrEmpty(isoPath))
        {
            throw new CommandLineException("ISO path cannot be null or empty", showUsage: false);
        }

        if (isoPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new CommandLineException("Invalid path characters detected", showUsage: false);
        }

        return isoPath;
    }

    private static bool MatchesAny(string value, string first, string second)
    {
        return string.Equals(value, first, StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, second, StringComparison.OrdinalIgnoreCase);
    }
}