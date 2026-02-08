using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;

namespace OnlyT.Avalonia.Utils;

/// <summary>
/// Version detection utility for checking application updates
/// </summary>
public static class VersionDetection
{
    /// <summary>
    /// URL to the latest release page (GitHub redirects this to the actual version)
    /// </summary>
    public static string LatestReleaseUrl => "https://github.com/lastowl/OnlyT/releases/latest";

    /// <summary>
    /// Gets the current application version
    /// </summary>
    public static Version? GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version;
    }

    /// <summary>
    /// Gets the current version as a string
    /// </summary>
    public static string GetCurrentVersionString()
    {
        var version = GetCurrentVersion();
        return version?.ToString() ?? "1.0.0.0";
    }

    /// <summary>
    /// Gets the latest release version string from GitHub by following the redirect
    /// </summary>
    public static async Task<string?> GetLatestReleaseVersionStringAsync()
    {
        try
        {
            using var client = new HttpClient();

            // Follow the redirect from /releases/latest to get actual version
            var response = await client.GetAsync(LatestReleaseUrl);
            if (response.IsSuccessStatusCode)
            {
                var latestVersionUri = response.RequestMessage?.RequestUri;
                if (latestVersionUri != null)
                {
                    var segments = latestVersionUri.Segments;
                    if (segments.Length > 0)
                    {
                        // Extract version from URL like .../releases/tag/v2.4.0.16
                        var versionSegment = segments[^1].TrimEnd('/');

                        // Remove 'v' prefix if present
                        if (versionSegment.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                        {
                            versionSegment = versionSegment[1..];
                        }

                        return versionSegment;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting latest release version from GitHub");
        }

        return null;
    }

    /// <summary>
    /// Gets the latest release version from GitHub
    /// </summary>
    public static async Task<Version?> GetLatestReleaseVersionAsync()
    {
        var versionString = await GetLatestReleaseVersionStringAsync();

        if (string.IsNullOrEmpty(versionString))
            return null;

        var tokens = versionString.Split('.');
        if (tokens.Length < 3)
            return null;

        // Parse version components (handles both X.X.X and X.X.X.X formats)
        if (!int.TryParse(tokens[0], out var major) ||
            !int.TryParse(tokens[1], out var minor) ||
            !int.TryParse(tokens[2], out var build))
            return null;

        var revision = 0;
        if (tokens.Length >= 4)
        {
            int.TryParse(tokens[3], out revision);
        }

        return new Version(major, minor, build, revision);
    }

    /// <summary>
    /// Checks if a new version is available
    /// </summary>
    public static async Task<bool> IsNewVersionAvailableAsync()
    {
        try
        {
            var latestVersion = await GetLatestReleaseVersionAsync();
            var currentVersion = GetCurrentVersion();

            if (latestVersion != null && currentVersion != null)
            {
                return latestVersion > currentVersion;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking for new version");
        }

        return false;
    }
}
