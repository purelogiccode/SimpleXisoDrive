namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Windows usage text builder.
/// </summary>
public class UsageTextTests
{
    /// <summary>
    /// Verifies the usage text includes the executable name and every option.
    /// </summary>
    [Fact]
    public void GetUsage_ContainsExecutableNameAndOptions()
    {
        var usage = UsageText.GetUsage("MyApp");

        Assert.Contains("Usage: MyApp <image-file> <mount-path> [options]", usage, StringComparison.Ordinal);
        Assert.Contains("-d, --debug", usage, StringComparison.Ordinal);
        Assert.Contains("-l, --launch", usage, StringComparison.Ordinal);
        Assert.Contains("-i, --image-iso", usage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the executable name is derived without throwing.
    /// </summary>
    [Fact]
    public void GetExecutableName_ReturnsNonEmptyName()
    {
        Assert.False(string.IsNullOrWhiteSpace(UsageText.GetExecutableName()));
    }
}
