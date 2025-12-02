namespace OnlyT.Avalonia.Services.NDI;

using global::Avalonia.Controls;

/// <summary>
/// Service for NDI (Network Device Interface) video output.
/// Enables streaming of the timer display to OBS/streaming software via NDI protocol.
/// </summary>
public interface INdiService : IDisposable
{
    /// <summary>
    /// Whether NDI output is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether NDI is currently sending frames
    /// </summary>
    bool IsSending { get; }

    /// <summary>
    /// The NDI source name that appears in NDI receivers
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Initialize NDI with the specified source name
    /// </summary>
    void Initialize(string sourceName);

    /// <summary>
    /// Start capturing and sending frames from the specified control
    /// </summary>
    void StartCapture(Control control, int width, int height, int frameRate = 30);

    /// <summary>
    /// Stop capturing and sending frames
    /// </summary>
    void StopCapture();

    /// <summary>
    /// Pause NDI output temporarily
    /// </summary>
    void Pause();

    /// <summary>
    /// Resume NDI output
    /// </summary>
    void Resume();
}
