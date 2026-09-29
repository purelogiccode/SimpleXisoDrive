using System.Text.Json.Serialization;

namespace SimpleXisoDrive.Core.Models;

/// <summary>
/// Request body sent to the application statistics API.
/// </summary>
internal sealed class StatsRequest
{
    /// <summary>Gets the unique identifier of the application.</summary>
    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; init; } = string.Empty;

    /// <summary>Gets the application version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}