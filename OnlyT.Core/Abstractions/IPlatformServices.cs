namespace OnlyT.Core.Abstractions;

/// <summary>
/// Platform-specific services abstraction
/// </summary>
public interface IPlatformServices
{
    IMonitorService MonitorService { get; }
    IWindowService WindowService { get; }
    ISystemIntegration SystemIntegration { get; }
}
