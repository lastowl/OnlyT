using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia.Platform.MacOS;

public class MacOSPlatformServices : IPlatformServices
{
    private readonly MacOSMonitorService _monitorService;
    private readonly MacOSWindowService _windowService;
    private readonly MacOSSystemIntegration _systemIntegration;

    public MacOSPlatformServices()
    {
        _monitorService = new MacOSMonitorService();
        _windowService = new MacOSWindowService();
        _systemIntegration = new MacOSSystemIntegration();
    }

    public IMonitorService MonitorService => _monitorService;
    public IWindowService WindowService => _windowService;
    public ISystemIntegration SystemIntegration => _systemIntegration;
}
