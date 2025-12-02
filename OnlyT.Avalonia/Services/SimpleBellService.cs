using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.Wave;
using Serilog;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Simple bell service using NAudio for cross-platform audio
/// </summary>
public class SimpleBellService : IBellService
{
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;
    private string? _bellSoundPath;
    private readonly bool _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private readonly bool _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    private bool _isPlaying;

    public SimpleBellService()
    {
        InitializeBellSound();
    }

    public bool IsPlaying => _isPlaying || (_wavePlayer?.PlaybackState == PlaybackState.Playing);

    private int _currentVolume = 70;

    public void Play(int volumePercent)
    {
        // Clamp volume to valid range
        _currentVolume = Math.Clamp(volumePercent, 0, 100);

        try
        {
            if (string.IsNullOrEmpty(_bellSoundPath) || !File.Exists(_bellSoundPath))
            {
                Log.Warning("Bell sound file not found at {Path}", _bellSoundPath);
                PlaySystemBeep();
                return;
            }

            // Use native player on macOS/Linux for better compatibility
            if (_isMacOS)
            {
                PlayOnMacOS(_bellSoundPath);
            }
            else if (_isLinux)
            {
                PlayOnLinux(_bellSoundPath);
            }
            else
            {
                PlayWithNAudio(_bellSoundPath);
            }

            Log.Information("Playing bell sound from {Path} at {Volume}% volume", _bellSoundPath, _currentVolume);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to play bell sound");
            PlaySystemBeep();
        }
    }

    public void Play()
    {
        Play(70); // Default volume
    }

    private void PlayWithNAudio(string path)
    {
        Stop(); // Stop any currently playing sound

        _audioFileReader = new AudioFileReader(path);
        _wavePlayer = new WaveOutEvent();
        _wavePlayer.Init(_audioFileReader);
        _wavePlayer.Volume = _currentVolume / 100f;
        _wavePlayer.PlaybackStopped += OnPlaybackStopped;
        _wavePlayer.Play();
    }

    private void PlayOnMacOS(string path)
    {
        // Use macOS native afplay command for better audio support
        // afplay -v takes volume from 0.0 to 1.0
        try
        {
            _isPlaying = true;
            var volume = _currentVolume / 100.0;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = $"-v {volume:F2} \"{path}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };
            process.Exited += (_, _) => _isPlaying = false;
            process.Start();
            Log.Information("Playing bell sound using afplay on macOS at {Volume}% volume", _currentVolume);
        }
        catch (Exception ex)
        {
            _isPlaying = false;
            Log.Warning(ex, "Failed to play sound with afplay, falling back to NAudio");
            PlayWithNAudio(path);
        }
    }

    private void PlayOnLinux(string path)
    {
        // Try common Linux audio players in order of preference
        // Volume settings vary by player
        var volumePercent = _currentVolume;
        var paVolume = (int)(volumePercent / 100.0 * 65536); // paplay uses 0-65536
        var ffVolume = volumePercent / 100.0; // ffplay uses 0.0-1.0

        var players = new[]
        {
            ("paplay", $"--volume={paVolume} \"{path}\""),  // PulseAudio
            ("aplay", $"\"{path}\""),                        // ALSA (no easy volume control)
            ("ffplay", $"-nodisp -autoexit -volume {ffVolume:F2} \"{path}\""), // FFmpeg
            ("mpv", $"--no-video --volume={volumePercent} \"{path}\"")  // MPV uses 0-100
        };

        foreach (var (player, args) in players)
        {
            try
            {
                _isPlaying = true;
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = player,
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                process.Exited += (_, _) => _isPlaying = false;
                process.Start();
                Log.Information("Playing bell sound using {Player} on Linux at {Volume}% volume", player, volumePercent);
                return;
            }
            catch (Exception)
            {
                // Player not available, try next one
                continue;
            }
        }

        _isPlaying = false;
        Log.Warning("No audio player found on Linux. Tried: paplay, aplay, ffplay, mpv");
        PlaySystemBeep();
    }

    private void PlaySystemBeep()
    {
        try
        {
            if (_isMacOS)
            {
                // macOS system beep
                Process.Start(new ProcessStartInfo
                {
                    FileName = "afplay",
                    Arguments = "/System/Library/Sounds/Glass.aiff",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else
            {
                Console.Beep(800, 500); // 800 Hz for 500ms
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "System beep not available");
        }
    }

    public void Stop()
    {
        try
        {
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _audioFileReader?.Dispose();
            _wavePlayer = null;
            _audioFileReader = null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error stopping bell sound");
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        _wavePlayer?.Dispose();
        _audioFileReader?.Dispose();
        _wavePlayer = null;
        _audioFileReader = null;
    }

    private void InitializeBellSound()
    {
        // Try to find a bell sound file in the application directory
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        Log.Information("Looking for bell sound in app directory: {AppDir}", appDir);

        var possiblePaths = new[]
        {
            Path.Combine(appDir, "Sounds", "bell.mp3"),
            Path.Combine(appDir, "Sounds", "bell.wav"),
            Path.Combine(appDir, "bell.mp3"),
            Path.Combine(appDir, "bell.wav"),
            // Also try the source directory during development
            Path.Combine(appDir, "..", "..", "..", "Sounds", "bell.mp3"),
            Path.Combine(appDir, "..", "..", "..", "..", "Sounds", "bell.mp3")
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            Log.Debug("Checking for bell sound at: {Path}", fullPath);

            if (File.Exists(fullPath))
            {
                _bellSoundPath = fullPath;
                Log.Information("Found bell sound at {Path}", fullPath);
                return;
            }
        }

        Log.Warning("No bell sound file found. Checked {Count} paths", possiblePaths.Length);
        foreach (var path in possiblePaths)
        {
            Log.Warning("  - {Path}", Path.GetFullPath(path));
        }
    }
}
