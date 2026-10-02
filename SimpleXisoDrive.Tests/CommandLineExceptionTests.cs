namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the command-line error type used by the Windows front end.
/// </summary>
public class CommandLineExceptionTests
{
    /// <summary>
    /// Verifies the message and the usage hint are both preserved.
    /// </summary>
    [Fact]
    public void Constructor_WithShowUsageTrue_SetsProperties()
    {
        var exception = new CommandLineException("unknown option", showUsage: true);

        Assert.Equal("unknown option", exception.Message);
        Assert.True(exception.ShowUsage);
    }

    /// <summary>
    /// Verifies the usage hint can be suppressed for errors that do not need it.
    /// </summary>
    [Fact]
    public void Constructor_WithShowUsageFalse_SetsProperties()
    {
        var exception = new CommandLineException("unexpected argument", showUsage: false);

        Assert.Equal("unexpected argument", exception.Message);
        Assert.False(exception.ShowUsage);
    }

    /// <summary>
    /// Verifies an empty message is preserved rather than replaced.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyMessage_PreservesIt()
    {
        Assert.Equal(string.Empty, new CommandLineException(string.Empty, showUsage: true).Message);
    }

    /// <summary>
    /// Verifies the type is catchable as <see cref="Exception"/>.
    /// </summary>
    [Fact]
    public void IsAssignableToException()
    {
        Assert.IsAssignableFrom<Exception>(new CommandLineException("x", showUsage: true));
    }
}
