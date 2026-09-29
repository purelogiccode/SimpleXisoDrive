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

    /// <summary>
    /// Verifies an exception without source or stack trace uses the documented placeholders.
    /// </summary>
    [Fact]
    public void BuildReport_WithoutStackTrace_UsesPlaceholders()
    {
        var report = BugReport.BuildReport("Error", "plain failure", new Exception("plain"));

        Assert.Contains("Type: System.Exception", report);
        Assert.Contains("Message: plain", report);
        Assert.Contains("Source: Unknown", report);
        Assert.Contains("StackTrace: Not available", report);
    }

    /// <summary>
    /// Verifies the report echoes the level and preserves multiline error text.
    /// </summary>
    [Fact]
    public void BuildReport_IncludesLevelAndMultilineError()
    {
        var report = BugReport.BuildReport("Fatal", "first line" + Environment.NewLine + "second line", null);

        Assert.Contains("Level: Fatal", report);
        Assert.Contains("Error message: first line", report);
        Assert.Contains("second line", report);
    }

    /// <summary>
    /// Verifies local error-log writes append the report and the separator line.
    /// </summary>
    [Fact]
    public void WriteLocalErrorLog_AppendsReportAndSeparator()
    {
        var marker = "test-report-" + Guid.NewGuid().ToString("N");

        BugReport.WriteLocalErrorLog(marker);

        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        var content = File.ReadAllText(logPath);
        Assert.Contains(marker, content, StringComparison.Ordinal);
        Assert.Contains("--------------------------------------------------", content, StringComparison.Ordinal);
    }
}
