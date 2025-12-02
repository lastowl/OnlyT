using System.Collections.Generic;
using Newtonsoft.Json;

namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Response data containing timer information (matches WPF original format)
/// </summary>
public class TimersResponseData
{
    [JsonProperty(PropertyName = "status")]
    public TimerStatus Status { get; set; } = new();

    [JsonProperty(PropertyName = "timerInfo")]
    public List<TimerInfo> TimerInfo { get; set; } = new();
}
