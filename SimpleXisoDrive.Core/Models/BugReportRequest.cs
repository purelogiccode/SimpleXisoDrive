using System.Text.Json.Serialization;

namespace SimpleXisoDrive.Core.Models;

/// <summary>
/// Request body sent to the remote bug report API.
/// </summary>
internal sealed class BugReportRequest
{
    /// <summary>Gets the full bug report message (required).</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Gets the name of the application reporting the bug.</summary>
    [JsonPropertyName("applicationName")]
    public string? ApplicationName { get; init; }

    /// <summary>Gets the application version.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>Gets additional user context.</summary>
    [JsonPropertyName("userInfo")]
    public string? UserInfo { get; init; }

    /// <summary>Gets the environment description (OS and runtime).</summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    /// <summary>Gets the exception stack trace, when available.</summary>
    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; init; }
}
