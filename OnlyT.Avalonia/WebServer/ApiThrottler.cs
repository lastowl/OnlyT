using System;
using System.Collections.Concurrent;
using System.Threading;
using Serilog;

namespace OnlyT.Avalonia.WebServer;

/// <summary>
/// Rate limiter to prevent API abuse
/// Tracks request counts per IP address within a sliding time window
/// </summary>
public interface IApiThrottler
{
    /// <summary>
    /// Check if a request from the given IP should be allowed
    /// </summary>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>True if request is allowed, false if throttled</returns>
    bool IsRequestAllowed(string ipAddress);

    /// <summary>
    /// Get remaining requests for an IP address in current window
    /// </summary>
    int GetRemainingRequests(string ipAddress);

    /// <summary>
    /// Reset throttling for all IPs (useful for testing)
    /// </summary>
    void Reset();
}

/// <summary>
/// Implementation of API throttling using a sliding window algorithm
/// </summary>
public class ApiThrottler : IApiThrottler, IDisposable
{
    private readonly ConcurrentDictionary<string, RequestTracker> _requestCounts = new();
    private readonly Timer _cleanupTimer;
    private readonly int _maxRequestsPerWindow;
    private readonly TimeSpan _windowDuration;
    private bool _disposed;

    /// <summary>
    /// Create a new API throttler
    /// </summary>
    /// <param name="maxRequestsPerWindow">Maximum requests allowed per time window (default: 100)</param>
    /// <param name="windowSeconds">Time window in seconds (default: 60)</param>
    public ApiThrottler(int maxRequestsPerWindow = 100, int windowSeconds = 60)
    {
        _maxRequestsPerWindow = maxRequestsPerWindow;
        _windowDuration = TimeSpan.FromSeconds(windowSeconds);

        // Clean up old entries every minute
        _cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public bool IsRequestAllowed(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return true; // Allow if we can't determine IP
        }

        var now = DateTime.UtcNow;
        var tracker = _requestCounts.GetOrAdd(ipAddress, _ => new RequestTracker());

        lock (tracker)
        {
            // Remove requests outside the window
            tracker.PruneOldRequests(now, _windowDuration);

            // Check if under limit
            if (tracker.RequestCount >= _maxRequestsPerWindow)
            {
                Log.Debug("Request throttled for IP {IpAddress}: {Count}/{Max} requests in window",
                    ipAddress, tracker.RequestCount, _maxRequestsPerWindow);
                return false;
            }

            // Add this request
            tracker.AddRequest(now);
            return true;
        }
    }

    public int GetRemainingRequests(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return _maxRequestsPerWindow;
        }

        if (!_requestCounts.TryGetValue(ipAddress, out var tracker))
        {
            return _maxRequestsPerWindow;
        }

        lock (tracker)
        {
            tracker.PruneOldRequests(DateTime.UtcNow, _windowDuration);
            return Math.Max(0, _maxRequestsPerWindow - tracker.RequestCount);
        }
    }

    public void Reset()
    {
        _requestCounts.Clear();
    }

    private void CleanupOldEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - _windowDuration - TimeSpan.FromMinutes(5); // Extra buffer

        foreach (var kvp in _requestCounts)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.LastRequestTime < cutoff)
                {
                    _requestCounts.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }

    /// <summary>
    /// Tracks requests for a single IP address
    /// </summary>
    private class RequestTracker
    {
        private readonly List<DateTime> _requestTimes = new();

        public int RequestCount => _requestTimes.Count;

        public DateTime LastRequestTime => _requestTimes.Count > 0
            ? _requestTimes[^1]
            : DateTime.MinValue;

        public void AddRequest(DateTime time)
        {
            _requestTimes.Add(time);
        }

        public void PruneOldRequests(DateTime now, TimeSpan window)
        {
            var cutoff = now - window;
            _requestTimes.RemoveAll(t => t < cutoff);
        }
    }
}
