using Newtonsoft.Json;

namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Culture data for API response (matches WPF original format)
/// </summary>
public class ApiCultureData
{
    [JsonProperty(PropertyName = "name")]
    public string? Name { get; set; }

    [JsonProperty(PropertyName = "isoCode2")]
    public string? IsoCode2 { get; set; }

    [JsonProperty(PropertyName = "isoCode3")]
    public string? IsoCode3 { get; set; }
}
