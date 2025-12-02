namespace OnlyT.Core.Abstractions;

/// <summary>
/// Service for OS-level system integration
/// </summary>
public interface ISystemIntegration
{
    /// <summary>
    /// Enable dark mode for a window
    /// </summary>
    void EnableDarkMode(object windowHandle, bool enable);

    /// <summary>
    /// Get a system icon (e.g., warning, error, info)
    /// </summary>
    object? GetSystemIcon(SystemIconType iconType);

    /// <summary>
    /// Check if dark mode is enabled system-wide
    /// </summary>
    bool IsDarkModeEnabled();

    /// <summary>
    /// Prevent multiple instances of the application
    /// </summary>
    bool EnsureSingleInstance(string mutexName);
}

public enum SystemIconType
{
    Warning,
    Error,
    Information,
    Question
}
