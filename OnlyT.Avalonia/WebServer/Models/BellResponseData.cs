using Newtonsoft.Json;

namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Response data for bell API calls
/// </summary>
public class BellResponseData
{
    /// <summary>
    /// Whether the bell was successfully triggered
    /// </summary>
    [JsonProperty(PropertyName = "success")]
    public bool Success { get; set; }

    /// <summary>
    /// Optional message describing the result
    /// </summary>
    [JsonProperty(PropertyName = "message")]
    public string? Message { get; set; }
}
