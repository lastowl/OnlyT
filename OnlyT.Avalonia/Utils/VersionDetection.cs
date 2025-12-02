using System.Reflection;

namespace OnlyT.Utils;

/// <summary>
/// Version detection utility
/// </summary>
public static class VersionDetection
{
    public static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0.0";
    }
}
