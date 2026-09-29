using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests that bug reports contain the required environment, error and exception sections.
/// </summary>
public class BugReportTests
{
    /// <summary>
    /// Verifies a report with an exception contains every required section and field.
    /// </summary>
    [Theory]
    [InlineData("Warning")]
    [InlineData("Error")]
    public void BuildReport_WithException_ContainsRequiredSections(string level)
    {
        var exception = new InvalidOperationException("boom");

        var report = BugReport.BuildReport(level, "Something failed", exception);

        Assert.Contains("=== Environment Details ===", report);
        Assert.Contains("Date: ", report);
        Assert.Contains("Application Name: SimpleXisoDrive", report);
        Assert.Contains("Application Version: ", report);
        Assert.Contains("OS Version: ", report);
        Assert.Contains("Architecture: ", report);
        Assert.Contains("Bitness: ", report);
        Assert.Contains("Processor Count: ", report);
        Assert.Contains("Base Directory: ", report);
        Assert.Contains("Temp Path: ", report);
        Assert.Contains("=== Error Details ===", report);
        Assert.Contains("Error message: Something failed", report);
        Assert.Contains("=== Exception Details ===", report);
        Assert.Contains("Type: System.InvalidOperationException", report);
        Assert.Contains("Message: boom", report);
        Assert.Contains("Source: ", report);
        Assert.Contains("StackTrace: ", report);
    }

    /// <summary>
    /// Verifies missing exception details are reported as None placeholders.
    /// </summary>
    [Fact]
    public void BuildReport_WithoutException_UsesNonePlaceholders()
    {
        var report = BugReport.BuildReport("Error", "No exception", null);

        Assert.Contains("Type: None", report);
        Assert.Contains("Message: None", report);
        Assert.Contains("Source: None", report);
        Assert.Contains("StackTrace: None", report);
    }

    /// <summary>
    /// Verifies the OS version line uses the current platform family name.
    /// </summary>
    [Fact]
    public void BuildReport_NamesTheOsFamilyVersionLine()
    {
        var report = BugReport.BuildReport("Error", "x", null);

        var expectedPrefix = OperatingSystem.IsWindows() ? "Windows Version: "
            : OperatingSystem.IsLinux() ? "Linux Version: "
            : "MacOsX Version: ";

        Assert.Contains(expectedPrefix, report);
    }
}
