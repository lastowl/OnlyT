namespace OnlyT.Avalonia.Services;

/// <summary>
/// Service for playing bell/alarm sounds
/// </summary>
public interface IBellService
{
    /// <summary>
    /// Whether the bell is currently playing
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Play the bell sound at specified volume
    /// </summary>
    /// <param name="volumePercent">Volume 0-100</param>
    void Play(int volumePercent);

    /// <summary>
    /// Play the bell sound at default volume (70%)
    /// </summary>
    void Play();

    /// <summary>
    /// Stop the bell sound
    /// </summary>
    void Stop();
}
