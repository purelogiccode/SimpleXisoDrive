namespace SimpleXisoDrive.Unix.Tests;

/// <summary>
/// Tests the Unix front end's program-name reporting used in the usage text.
/// </summary>
public class ProgramNameTests
{
    /// <summary>
    /// Verifies the usage name is derived from the running executable and never empty.
    /// </summary>
    [Fact]
    public void GetExecutableName_ReturnsNonEmptyName()
    {
        var name = Program.GetExecutableName();

        Assert.False(string.IsNullOrWhiteSpace(name));
    }
}