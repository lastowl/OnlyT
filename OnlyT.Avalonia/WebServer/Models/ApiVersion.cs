using Newtonsoft.Json;

namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// API version information (matches WPF original format)
/// </summary>
public class ApiVersion
{
    [JsonProperty(PropertyName = "lowVersion")]
    public int LowVersion { get; set; }

    [JsonProperty(PropertyName = "highVersion")]
    public int HighVersion { get; set; }
}
