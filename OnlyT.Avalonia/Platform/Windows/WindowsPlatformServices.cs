using OnlyT.Core.Abstractions;

namespace OnlyT.Avalonia.Platform.Windows;

public class WindowsPlatformServices : IPlatformServices
{
    private readonly WindowsMonitorService _monitorService;
    private readonly WindowsWindowService _windowService;
    private readonly WindowsSystemIntegration _systemIntegration;

    public WindowsPlatformServices()
    {
        _monitorService = new WindowsMonitorService();
        _windowService = new WindowsWindowService();
        _systemIntegration = new WindowsSystemIntegration();
    }

    public IMonitorService MonitorService => _monitorService;
    public IWindowService WindowService => _windowService;
    public ISystemIntegration SystemIntegration => _systemIntegration;
}
