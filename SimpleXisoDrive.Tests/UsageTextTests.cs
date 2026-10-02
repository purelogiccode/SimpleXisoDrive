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
        Assert.Contains("-h, --help", usage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the executable name is derived without throwing.
    /// </summary>
    [Fact]
    public void GetExecutableName_ReturnsNonEmptyName()
    {
        Assert.False(string.IsNullOrWhiteSpace(UsageText.GetExecutableName()));
    }

    /// <summary>
    /// Verifies the executable name has no file extension.
    /// </summary>
    [Fact]
    public void GetExecutableName_HasNoExtension()
    {
        Assert.DoesNotContain('.', UsageText.GetExecutableName());
    }

    /// <summary>
    /// Verifies the usage text describes both arguments and the supported formats.
    /// </summary>
    [Fact]
    public void GetUsage_DescribesArgumentsAndFormats()
    {
        var usage = UsageText.GetUsage("App");

        Assert.Contains("<image-file>", usage, StringComparison.Ordinal);
        Assert.Contains("<mount-path>", usage, StringComparison.Ordinal);
        Assert.Contains(".iso", usage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".chd", usage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".zar", usage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("xemu", usage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the supplied executable name is used in the usage line.
    /// </summary>
    [Fact]
    public void GetUsage_UsesSuppliedExecutableName()
    {
        Assert.Contains("Usage: CustomName <image-file>", UsageText.GetUsage("CustomName"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the usage text ends with a newline so console output does not run on.
    /// </summary>
    [Fact]
    public void GetUsage_EndsWithNewLine()
    {
        Assert.EndsWith(Environment.NewLine, UsageText.GetUsage("App"), StringComparison.Ordinal);
    }
}