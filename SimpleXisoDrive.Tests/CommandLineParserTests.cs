namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Windows command-line parser extracted from the entry point.
/// </summary>
public class CommandLineParserTests
{
    /// <summary>
    /// Verifies a single argument enters drag-and-drop mode with launch implied.
    /// </summary>
    [Fact]
    public void Parse_SingleArgument_EntersDragAndDropMode()
    {
        var arguments = CommandLineParser.Parse(["game.iso"]);

        Assert.True(arguments.IsDragAndDrop);
        Assert.True(arguments.Launch);
        Assert.Equal("game.iso", arguments.ImagePath);
        Assert.Null(arguments.MountPath);
    }

    /// <summary>
    /// Verifies options are matched case-insensitively and position-independently.
    /// </summary>
    [Fact]
    public void Parse_TwoArguments_ParsesOptionsCaseInsensitively()
    {
        var arguments = CommandLineParser.Parse(["game.iso", "M:\\", "-D", "--launch", "-i"]);

        Assert.False(arguments.IsDragAndDrop);
        Assert.Equal("game.iso", arguments.ImagePath);
        Assert.Equal("M:\\", arguments.MountPath);
        Assert.True(arguments.Debug);
        Assert.True(arguments.Launch);
        Assert.True(arguments.ImageIso);
    }

    /// <summary>
    /// Verifies missing options leave every flag off.
    /// </summary>
    [Fact]
    public void Parse_WithoutOptions_LeavesFlagsOff()
    {
        var arguments = CommandLineParser.Parse(["game.iso", "M:\\"]);

        Assert.False(arguments.Debug);
        Assert.False(arguments.Launch);
        Assert.False(arguments.ImageIso);
    }

    /// <summary>
    /// Verifies an empty image path is rejected without a usage dump.
    /// </summary>
    [Fact]
    public void Parse_EmptyImagePath_ThrowsCommandLineException()
    {
        var ex = Assert.Throws<CommandLineException>(() => CommandLineParser.Parse([string.Empty, "M:\\"]));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(ex.ShowUsage);
    }

    /// <summary>
    /// Verifies invalid path characters are rejected.
    /// </summary>
    [Fact]
    public void Parse_InvalidPathCharacters_ThrowsCommandLineException()
    {
        var ex = Assert.Throws<CommandLineException>(() => CommandLineParser.Parse(["\0bad", "M:\\"]));

        Assert.Contains("Invalid path", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies unknown options fail with a usage hint.
    /// </summary>
    [Fact]
    public void Parse_UnknownOption_ThrowsWithUsageHint()
    {
        var ex = Assert.Throws<CommandLineException>(() =>
            CommandLineParser.Parse(["game.iso", "M:\\", "--nope"]));

        Assert.Contains("--nope", ex.Message, StringComparison.Ordinal);
        Assert.True(ex.ShowUsage);
    }

    /// <summary>
    /// Verifies extra positional arguments fail without a usage dump.
    /// </summary>
    [Fact]
    public void Parse_ExtraPositional_ThrowsWithoutUsageHint()
    {
        var ex = Assert.Throws<CommandLineException>(() =>
            CommandLineParser.Parse(["game.iso", "M:\\", "extra"]));

        Assert.Contains("extra", ex.Message, StringComparison.Ordinal);
        Assert.False(ex.ShowUsage);
    }

    /// <summary>
    /// Verifies a null argument array is rejected.
    /// </summary>
    [Fact]
    public void Parse_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CommandLineParser.Parse(null!));
    }

    /// <summary>
    /// Verifies options may appear before the mount path, matching the Unix front end.
    /// </summary>
    [Fact]
    public void Parse_OptionBeforeMountPath_ParsesBoth()
    {
        var arguments = CommandLineParser.Parse(["game.iso", "-d", "M:\\"]);

        Assert.False(arguments.IsDragAndDrop);
        Assert.Equal("M:\\", arguments.MountPath);
        Assert.True(arguments.Debug);
    }

    /// <summary>
    /// Verifies a single argument plus an option stays in drag-and-drop mode with the
    /// option applied and launch implied.
    /// </summary>
    [Fact]
    public void Parse_SingleArgumentWithOption_EntersDragAndDropMode()
    {
        var arguments = CommandLineParser.Parse(["game.iso", "--image-iso"]);

        Assert.True(arguments.IsDragAndDrop);
        Assert.True(arguments.Launch);
        Assert.True(arguments.ImageIso);
        Assert.Null(arguments.MountPath);
    }

    /// <summary>
    /// Verifies help flags are recognized case-insensitively.
    /// </summary>
    /// <param name="argument">The help argument to test.</param>
    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-H")]
    [InlineData("--HELP")]
    public void IsHelpOption_RecognizesHelpCaseInsensitively(string argument)
    {
        Assert.True(CommandLineParser.IsHelpOption(argument));
    }

    /// <summary>
    /// Verifies non-help arguments are not treated as help.
    /// </summary>
    /// <param name="argument">The argument to test.</param>
    [Theory]
    [InlineData("-d")]
    [InlineData("help")]
    [InlineData("--debug")]
    [InlineData("")]
    public void IsHelpOption_RejectsOtherArguments(string argument)
    {
        Assert.False(CommandLineParser.IsHelpOption(argument));
    }
}