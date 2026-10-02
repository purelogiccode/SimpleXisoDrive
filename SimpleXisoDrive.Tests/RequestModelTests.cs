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

    /// <summary>
    /// Verifies omitted optional fields serialize as JSON null rather than being dropped.
    /// </summary>
    [Fact]
    public void BugReportRequest_OptionalFieldsDefaultToNull()
    {
        var request = new BugReportRequest { Message = "only the required field" };

        Assert.Null(request.ApplicationName);
        Assert.Null(request.Version);
        Assert.Null(request.UserInfo);
        Assert.Null(request.Environment);
        Assert.Null(request.StackTrace);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request));
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("stackTrace").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("userInfo").ValueKind);
    }

    /// <summary>
    /// Verifies an API-shaped bug report payload deserializes with every field mapped.
    /// </summary>
    [Fact]
    public void BugReportRequest_DeserializesApiShapedJson()
    {
        const string json = """
                            {"message":"m","applicationName":"app","version":"2.0","userInfo":"u",
                             "environment":"e","stackTrace":"s"}
                            """;

        var request = JsonSerializer.Deserialize<BugReportRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("m", request.Message);
        Assert.Equal("app", request.ApplicationName);
        Assert.Equal("2.0", request.Version);
        Assert.Equal("u", request.UserInfo);
        Assert.Equal("e", request.Environment);
        Assert.Equal("s", request.StackTrace);
    }

    /// <summary>
    /// Verifies an API-shaped statistics payload deserializes with both fields mapped.
    /// </summary>
    [Fact]
    public void StatsRequest_DeserializesApiShapedJson()
    {
        var request = JsonSerializer.Deserialize<StatsRequest>(
            "{\"applicationId\":\"simplexisodrive\",\"version\":\"1.4.0\"}");

        Assert.NotNull(request);
        Assert.Equal("simplexisodrive", request.ApplicationId);
        Assert.Equal("1.4.0", request.Version);
    }

    /// <summary>
    /// Verifies unknown JSON properties are ignored instead of failing deserialization.
    /// </summary>
    [Fact]
    public void BugReportRequest_IgnoresUnknownProperties()
    {
        const string json = "{\"message\":\"m\",\"unexpected\":\"ignored\"}";

        var request = JsonSerializer.Deserialize<BugReportRequest>(json);

        Assert.NotNull(request);
        Assert.Equal("m", request.Message);
    }
}