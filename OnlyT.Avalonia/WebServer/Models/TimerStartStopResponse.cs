using Newtonsoft.Json;

namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Response data for timer start/stop operations
/// </summary>
public class TimerStartStopResponse
{
    [JsonProperty(PropertyName = "success")]
    public bool Success { get; set; }

    [JsonProperty(PropertyName = "talkId")]
    public int TalkId { get; set; }

    [JsonProperty(PropertyName = "command")]
    public string Command { get; set; } = string.Empty;

    [JsonProperty(PropertyName = "status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty(PropertyName = "currentStatus")]
    public TimerStatus? CurrentStatus { get; set; }

    [JsonProperty(PropertyName = "errorMessage")]
    public string? ErrorMessage { get; set; }
}
