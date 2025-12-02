namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Response data for timer start/stop operations
/// </summary>
public class TimerStartStopResponse
{
    public bool Success { get; set; }
    public int TalkId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
