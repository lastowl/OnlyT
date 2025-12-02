namespace OnlyT.Core.Abstractions;

/// <summary>
/// Service for window management operations
/// </summary>
public interface IWindowService
{
    /// <summary>
    /// Save window placement (position, size, state)
    /// </summary>
    void SaveWindowPlacement(string windowId, WindowPlacement placement);

    /// <summary>
    /// Restore window placement (position, size, state)
    /// </summary>
    WindowPlacement? RestoreWindowPlacement(string windowId);

    /// <summary>
    /// Set window to always on top
    /// </summary>
    void SetAlwaysOnTop(object windowHandle, bool alwaysOnTop);

    /// <summary>
    /// Hide window close button
    /// </summary>
    void HideCloseButton(object windowHandle);
}

/// <summary>
/// Window placement information
/// </summary>
public class WindowPlacement
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public WindowState State { get; set; }
}

public enum WindowState
{
    Normal,
    Minimized,
    Maximized
}
