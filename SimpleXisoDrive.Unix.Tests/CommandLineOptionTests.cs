namespace SimpleXisoDrive.Unix.Tests;

/// <summary>
/// Tests the Unix front end's option validation. Option matching is case-insensitive,
/// matching the Windows front end and the documented command-line behavior.
/// </summary>
public class CommandLineOptionTests
{
    /// <summary>
    /// Verifies every documented option flag is accepted, in any letter case.
    /// </summary>
    /// <param name="option">The option flag to test.</param>
    [Theory]
    [InlineData("-d")]
    [InlineData("--debug")]
    [InlineData("-D")]
    [InlineData("--DEBUG")]
    [InlineData("-l")]
    [InlineData("--launch")]
    [InlineData("--Launch")]
    [InlineData("-i")]
    [InlineData("--image-iso")]
    [InlineData("--Image-Iso")]
    public void IsKnownOption_AcceptsDocumentedOptionsCaseInsensitively(string option)
    {
        Assert.True(Program.IsKnownOption(option));
    }

    /// <summary>
    /// Verifies unknown and malformed flags are rejected.
    /// </summary>
    /// <param name="option">The option flag to test.</param>
    [Theory]
    [InlineData("-x")]
    [InlineData("--unknown")]
    [InlineData("--image")]
    [InlineData("-")]
    [InlineData("")]
    public void IsKnownOption_RejectsUnknownOptions(string option)
    {
        Assert.False(Program.IsKnownOption(option));
    }
}
