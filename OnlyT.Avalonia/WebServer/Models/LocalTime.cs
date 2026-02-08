using Newtonsoft.Json;

namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Local time data for API response (matches WPF original format)
/// </summary>
public class LocalTime
{
    [JsonProperty(PropertyName = "year")]
    public int Year { get; set; }

    [JsonProperty(PropertyName = "month")]
    public int Month { get; set; }

    [JsonProperty(PropertyName = "day")]
    public int Day { get; set; }

    [JsonProperty(PropertyName = "hour")]
    public int Hour { get; set; }

    [JsonProperty(PropertyName = "min")]
    public int Min { get; set; }

    [JsonProperty(PropertyName = "second")]
    public int Sec { get; set; }

    [JsonProperty(PropertyName = "timeString24")]
    public string TimeString24 { get; set; } = string.Empty;

    [JsonProperty(PropertyName = "timeString12")]
    public string TimeString12 { get; set; } = string.Empty;
}
