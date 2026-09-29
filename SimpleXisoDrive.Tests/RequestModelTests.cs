using System.Text.Json;
using SimpleXisoDrive.Core.Models;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the JSON wire format of the bug report and statistics request bodies.
/// </summary>
public class RequestModelTests
{
    /// <summary>
    /// Verifies the statistics request serializes with the API's property names.
    /// </summary>
    [Fact]
    public void StatsRequest_SerializesExpectedPropertyNames()
    {
        var request = new StatsRequest
        {
            ApplicationId = "simplexisodrive",
            Version = "1.4.0"
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var root = document.RootElement;

        Assert.Equal("simplexisodrive", root.GetProperty("applicationId").GetString());
        Assert.Equal("1.4.0", root.GetProperty("version").GetString());
    }

    /// <summary>
    /// Verifies the statistics request defaults to empty strings.
    /// </summary>
    [Fact]
    public void StatsRequest_DefaultsToEmptyStrings()
    {
        var request = new StatsRequest();

        Assert.Equal(string.Empty, request.ApplicationId);
        Assert.Equal(string.Empty, request.Version);
    }

    /// <summary>
    /// Verifies the bug report request serializes every field with the API's property names.
    /// </summary>
    [Fact]
    public void BugReportRequest_SerializesAllProperties()
    {
        var request = new BugReportRequest
        {
            Message = "report body",
            ApplicationName = "SimpleXisoDrive",
            Version = "1.4.0",
            UserInfo = "tester",
            Environment = "Windows (X64)",
            StackTrace = "at Example()"
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var root = document.RootElement;

        Assert.Equal("report body", root.GetProperty("message").GetString());
        Assert.Equal("SimpleXisoDrive", root.GetProperty("applicationName").GetString());
        Assert.Equal("1.4.0", root.GetProperty("version").GetString());
        Assert.Equal("tester", root.GetProperty("userInfo").GetString());
        Assert.Equal("Windows (X64)", root.GetProperty("environment").GetString());
        Assert.Equal("at Example()", root.GetProperty("stackTrace").GetString());
    }
}
