using System;
using Serilog;

namespace OnlyT.EventTracking;

/// <summary>
/// Simple event tracker stub for cross-platform version
/// Logs to Serilog instead of external telemetry
/// </summary>
public static class EventTracker
{
    public static void Error(Exception ex, string message)
    {
        Log.Error(ex, message);
    }

    public static void Track(string eventName)
    {
        Log.Information("Event: {EventName}", eventName);
    }

    public static void Track(string eventName, object properties)
    {
        Log.Information("Event: {EventName}, Properties: {@Properties}", eventName, properties);
    }
}
