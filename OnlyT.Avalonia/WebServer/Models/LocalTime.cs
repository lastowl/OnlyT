namespace OnlyT.Avalonia.WebServer.Models;

/// <summary>
/// Local time data for API response
/// </summary>
public class LocalTime
{
    public int Hour { get; set; }
    public int Min { get; set; }
    public int Sec { get; set; }
    public string TimeString24 { get; set; } = string.Empty;
    public string TimeString12 { get; set; } = string.Empty;
}
