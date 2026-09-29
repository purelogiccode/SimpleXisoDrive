namespace SimpleXisoDrive;

/// <summary>
/// Parses and validates the Windows command line. Extracted from the entry point so the
/// argument rules can be unit tested.
/// </summary>
internal static class CommandLineParser
{
    /// <summary>
    /// Parses the supplied arguments. At least one argument is required; the zero-argument
    /// case (usage display) is handled by the caller.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The validated arguments.</returns>
    /// <exception cref="CommandLineException">Thrown when an argument is invalid.</exception>
    public static CommandLineArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 1)
        {
            return new CommandLineArguments
            {
                ImagePath = ValidateImagePath(args[0]),
                IsDragAndDrop = true,
                Launch = true
            };
        }

        var imagePath = ValidateImagePath(args[0]);
        var debug = false;
        var launch = false;
        var imageIso = false;

        foreach (var argument in args.Skip(2))
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
            else
            {
                throw new CommandLineException($"unexpected argument '{argument}'.", showUsage: false);
            }
        }

        return new CommandLineArguments
        {
            ImagePath = imagePath,
            MountPath = args[1],
            Debug = debug,
            Launch = launch,
            ImageIso = imageIso
        };
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
