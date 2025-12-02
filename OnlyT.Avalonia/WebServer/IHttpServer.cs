using System;

namespace OnlyT.Avalonia.WebServer;

/// <summary>
/// HTTP server interface for remote control API
/// </summary>
public interface IHttpServer : IDisposable
{
    /// <summary>
    /// Start the HTTP server on the specified port
    /// </summary>
    void Start(int port);

    /// <summary>
    /// Stop the HTTP server
    /// </summary>
    void Stop();

    /// <summary>
    /// Whether the server is currently running
    /// </summary>
    bool IsRunning { get; }
}
