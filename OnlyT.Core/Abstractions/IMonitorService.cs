namespace OnlyT.Core.Abstractions;

/// <summary>
/// Service for detecting and managing multiple monitors/displays
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// Get all available monitors/displays
    /// </summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>
    /// Get the primary monitor
    /// </summary>
    MonitorInfo GetPrimaryMonitor();
}

/// <summary>
/// Information about a monitor/display
/// </summary>
public class MonitorInfo
{
    public string MonitorId { get; set; } = string.Empty;
    public string MonitorName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
