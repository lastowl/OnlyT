using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia.Platform.Linux;

public class LinuxPlatformServices : IPlatformServices
{
    private readonly LinuxMonitorService _monitorService;
    private readonly LinuxWindowService _windowService;
    private readonly LinuxSystemIntegration _systemIntegration;

    public LinuxPlatformServices()
    {
        _monitorService = new LinuxMonitorService();
        _windowService = new LinuxWindowService();
        _systemIntegration = new LinuxSystemIntegration();
    }

    public IMonitorService MonitorService => _monitorService;
    public IWindowService WindowService => _windowService;
    public ISystemIntegration SystemIntegration => _systemIntegration;
}
