using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace OnlyT.Avalonia.Services;

/// <summary>
/// Cross-platform firewall configuration service
/// Supports Windows (netsh), Linux/Debian/Ubuntu (ufw), and macOS (handled via plist)
/// </summary>
public interface IFirewallService
{
    /// <summary>
    /// Configure firewall to allow incoming connections on the specified port
    /// </summary>
    FirewallResult ConfigureFirewall(int port);

    /// <summary>
    /// Check if firewall configuration is likely needed
    /// </summary>
    bool IsFirewallConfigurationNeeded();

    /// <summary>
    /// Get platform-specific instructions for manual firewall configuration
    /// </summary>
    string GetManualInstructions(int port);

    /// <summary>
    /// Get the current firewall/network permissions status
    /// </summary>
    FirewallStatus GetFirewallStatus(int port);

    /// <summary>
    /// Trigger macOS local network access permission prompt
    /// </summary>
    void RequestLocalNetworkAccess();
}

public class FirewallStatus
{
    public bool IsConfigured { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string PlatformName { get; set; } = string.Empty;
    public bool CanConfigure { get; set; }
}

public class FirewallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool RequiresElevation { get; set; }
    public bool AlreadyConfigured { get; set; }
}

public class FirewallService : IFirewallService
{
    private const string RuleName = "OnlyT-WebServer";

    public FirewallResult ConfigureFirewall(int port)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ConfigureWindowsFirewall(port);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ConfigureLinuxFirewall(port);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS is handled via Info.plist and Entitlements.plist
            return new FirewallResult
            {
                Success = true,
                Message = "macOS firewall is configured via application entitlements.",
                AlreadyConfigured = true
            };
        }

        return new FirewallResult
        {
            Success = false,
            Message = "Unsupported platform for automatic firewall configuration."
        };
    }

    public bool IsFirewallConfigurationNeeded()
    {
        // On macOS, the plist handles it
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        // On Windows and Linux, firewall config may be needed
        return true;
    }

    public string GetManualInstructions(int port)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $@"Windows Firewall Configuration:
Run these commands as Administrator in Command Prompt:

netsh advfirewall firewall add rule name=""{RuleName}"" dir=in action=allow protocol=TCP localport={port}

To remove the rule later:
netsh advfirewall firewall delete rule name=""{RuleName}""";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $@"Linux (Debian/Ubuntu) Firewall Configuration:
Run these commands with sudo:

sudo ufw allow {port}/tcp comment 'OnlyT Web Server'
sudo ufw reload

To remove the rule later:
sudo ufw delete allow {port}/tcp

For systems using iptables directly:
sudo iptables -A INPUT -p tcp --dport {port} -j ACCEPT";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $@"macOS Firewall Configuration:
The application should automatically request network permissions.
If prompted, click 'Allow' to permit incoming connections.

If issues persist, go to:
System Preferences > Security & Privacy > Firewall > Firewall Options
And add OnlyT to the allowed applications.";
        }

        return "Please configure your firewall to allow incoming TCP connections on port " + port;
    }

    private FirewallResult ConfigureWindowsFirewall(int port)
    {
        try
        {
            // First, try to delete any existing rule
            RunProcess("netsh", $"advfirewall firewall delete rule name=\"{RuleName}\"", out _, out _);

            // Add the firewall rule
            var exitCode = RunProcess("netsh",
                $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport={port}",
                out var output, out var error);

            if (exitCode == 0)
            {
                Log.Information("Windows firewall rule added for port {Port}", port);
                return new FirewallResult
                {
                    Success = true,
                    Message = $"Firewall rule '{RuleName}' added for port {port}."
                };
            }

            // Check if it's an elevation issue
            if (error.Contains("requires elevation") || error.Contains("Access is denied"))
            {
                Log.Warning("Windows firewall configuration requires elevation");
                return new FirewallResult
                {
                    Success = false,
                    Message = "Administrator privileges required to configure Windows Firewall.",
                    RequiresElevation = true
                };
            }

            Log.Warning("Failed to configure Windows firewall: {Error}", error);
            return new FirewallResult
            {
                Success = false,
                Message = $"Failed to configure firewall: {error}"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error configuring Windows firewall");
            return new FirewallResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private FirewallResult ConfigureLinuxFirewall(int port)
    {
        // Check if ufw is available (Debian/Ubuntu)
        if (IsCommandAvailable("ufw"))
        {
            return ConfigureUfw(port);
        }

        // Check if iptables is available
        if (IsCommandAvailable("iptables"))
        {
            return ConfigureIptables(port);
        }

        return new FirewallResult
        {
            Success = false,
            Message = "No supported firewall tool found (ufw or iptables)."
        };
    }

    private FirewallResult ConfigureUfw(int port)
    {
        try
        {
            // Check if ufw is active
            var statusCode = RunProcess("ufw", "status", out var statusOutput, out _);

            if (!statusOutput.Contains("Status: active"))
            {
                Log.Information("UFW is not active, firewall configuration may not be needed");
                return new FirewallResult
                {
                    Success = true,
                    Message = "UFW firewall is not active. No configuration needed.",
                    AlreadyConfigured = true
                };
            }

            // Check if rule already exists
            if (statusOutput.Contains(port.ToString()))
            {
                Log.Information("UFW rule for port {Port} already exists", port);
                return new FirewallResult
                {
                    Success = true,
                    Message = $"Firewall rule for port {port} already exists.",
                    AlreadyConfigured = true
                };
            }

            // Try to add the rule (requires sudo)
            var exitCode = RunProcess("sudo", $"ufw allow {port}/tcp comment 'OnlyT Web Server'",
                out var output, out var error);

            if (exitCode == 0)
            {
                Log.Information("UFW rule added for port {Port}", port);
                return new FirewallResult
                {
                    Success = true,
                    Message = $"UFW rule added for port {port}."
                };
            }

            // Likely needs sudo password
            if (error.Contains("sudo") || error.Contains("password"))
            {
                return new FirewallResult
                {
                    Success = false,
                    Message = "Root privileges required. Please run: sudo ufw allow " + port + "/tcp",
                    RequiresElevation = true
                };
            }

            return new FirewallResult
            {
                Success = false,
                Message = $"Failed to configure UFW: {error}"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error configuring UFW");
            return new FirewallResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private FirewallResult ConfigureIptables(int port)
    {
        try
        {
            // Check if rule exists
            var checkCode = RunProcess("iptables", $"-C INPUT -p tcp --dport {port} -j ACCEPT",
                out _, out _);

            if (checkCode == 0)
            {
                return new FirewallResult
                {
                    Success = true,
                    Message = $"iptables rule for port {port} already exists.",
                    AlreadyConfigured = true
                };
            }

            // Try to add rule
            var exitCode = RunProcess("sudo", $"iptables -A INPUT -p tcp --dport {port} -j ACCEPT",
                out _, out var error);

            if (exitCode == 0)
            {
                Log.Information("iptables rule added for port {Port}", port);
                return new FirewallResult
                {
                    Success = true,
                    Message = $"iptables rule added for port {port}."
                };
            }

            return new FirewallResult
            {
                Success = false,
                Message = $"Failed to configure iptables: {error}",
                RequiresElevation = true
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error configuring iptables");
            return new FirewallResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public FirewallStatus GetFirewallStatus(int port)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsFirewallStatus(port);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxFirewallStatus(port);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacOSFirewallStatus();
        }

        return new FirewallStatus
        {
            IsConfigured = false,
            StatusMessage = "Unknown platform",
            PlatformName = "Unknown",
            CanConfigure = false
        };
    }

    public void RequestLocalNetworkAccess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            // On macOS, we trigger the local network permission prompt by attempting
            // to create a socket connection to a local network address.
            // This forces the system to show the "allow local network access" dialog.
            Log.Information("Requesting macOS local network access permission");

            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);

            // Bind to any available port
            socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));

            // Try to send a UDP packet to a local broadcast address
            // This triggers the local network permission prompt on macOS
            var broadcastEndpoint = new System.Net.IPEndPoint(
                System.Net.IPAddress.Parse("224.0.0.1"),
                9999);

            socket.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.Socket,
                System.Net.Sockets.SocketOptionName.Broadcast,
                true);

            var data = new byte[] { 0x00 };
            socket.SendTo(data, broadcastEndpoint);

            Log.Information("Local network access request sent - permission dialog should appear if not already granted");
        }
        catch (Exception ex)
        {
            // Permission was likely denied or dialog was shown
            Log.Information("Local network access request completed: {Message}", ex.Message);
        }
    }

    private FirewallStatus GetWindowsFirewallStatus(int port)
    {
        try
        {
            // Check if the firewall rule exists
            var exitCode = RunProcess("netsh",
                $"advfirewall firewall show rule name=\"{RuleName}\"",
                out var output, out _);

            if (exitCode == 0 && output.Contains(RuleName))
            {
                return new FirewallStatus
                {
                    IsConfigured = true,
                    StatusMessage = $"Firewall rule '{RuleName}' is configured for port {port}.",
                    PlatformName = "Windows Firewall",
                    CanConfigure = true
                };
            }

            return new FirewallStatus
            {
                IsConfigured = false,
                StatusMessage = "Firewall rule not configured. Remote access may be blocked.",
                PlatformName = "Windows Firewall",
                CanConfigure = true
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking Windows firewall status");
            return new FirewallStatus
            {
                IsConfigured = false,
                StatusMessage = $"Unable to check firewall status: {ex.Message}",
                PlatformName = "Windows Firewall",
                CanConfigure = false
            };
        }
    }

    private FirewallStatus GetLinuxFirewallStatus(int port)
    {
        try
        {
            // Check if ufw is available and active
            if (IsCommandAvailable("ufw"))
            {
                var statusCode = RunProcess("ufw", "status", out var statusOutput, out _);

                if (!statusOutput.Contains("Status: active"))
                {
                    return new FirewallStatus
                    {
                        IsConfigured = true,
                        StatusMessage = "UFW firewall is not active. No configuration needed.",
                        PlatformName = "Linux (UFW)",
                        CanConfigure = true
                    };
                }

                if (statusOutput.Contains(port.ToString()))
                {
                    return new FirewallStatus
                    {
                        IsConfigured = true,
                        StatusMessage = $"UFW rule for port {port} is configured.",
                        PlatformName = "Linux (UFW)",
                        CanConfigure = true
                    };
                }

                return new FirewallStatus
                {
                    IsConfigured = false,
                    StatusMessage = $"UFW is active but port {port} is not allowed.",
                    PlatformName = "Linux (UFW)",
                    CanConfigure = true
                };
            }

            // Check iptables
            if (IsCommandAvailable("iptables"))
            {
                var checkCode = RunProcess("iptables", $"-C INPUT -p tcp --dport {port} -j ACCEPT",
                    out _, out _);

                if (checkCode == 0)
                {
                    return new FirewallStatus
                    {
                        IsConfigured = true,
                        StatusMessage = $"iptables rule for port {port} is configured.",
                        PlatformName = "Linux (iptables)",
                        CanConfigure = true
                    };
                }

                return new FirewallStatus
                {
                    IsConfigured = false,
                    StatusMessage = $"iptables rule for port {port} not found.",
                    PlatformName = "Linux (iptables)",
                    CanConfigure = true
                };
            }

            return new FirewallStatus
            {
                IsConfigured = true,
                StatusMessage = "No firewall detected (ufw/iptables not found).",
                PlatformName = "Linux",
                CanConfigure = false
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking Linux firewall status");
            return new FirewallStatus
            {
                IsConfigured = false,
                StatusMessage = $"Unable to check firewall status: {ex.Message}",
                PlatformName = "Linux",
                CanConfigure = false
            };
        }
    }

    private FirewallStatus GetMacOSFirewallStatus()
    {
        try
        {
            // On macOS, we can't directly check if local network permission is granted
            // But we can check if the application firewall is enabled
            var exitCode = RunProcess("/usr/libexec/ApplicationFirewall/socketfilterfw",
                "--getglobalstate",
                out var output, out _);

            var firewallEnabled = output.Contains("enabled");

            // We can't programmatically check local network permission status on macOS
            // The user needs to check System Settings > Privacy & Security > Local Network
            return new FirewallStatus
            {
                IsConfigured = true, // Assume configured via entitlements
                StatusMessage = firewallEnabled
                    ? "macOS Firewall is enabled. Ensure OnlyT is allowed in Privacy settings."
                    : "macOS Firewall is disabled. Local Network permission may still be required.",
                PlatformName = "macOS",
                CanConfigure = false // macOS firewall is configured via System Settings
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking macOS firewall status");
            return new FirewallStatus
            {
                IsConfigured = true,
                StatusMessage = "Unable to check firewall status. Ensure Local Network access is granted in System Settings.",
                PlatformName = "macOS",
                CanConfigure = false
            };
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var exitCode = RunProcess("which", command, out var output, out _);
            return exitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private static int RunProcess(string fileName, string arguments, out string output, out string error)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            output = process.StandardOutput.ReadToEnd();
            error = process.StandardError.ReadToEnd();

            process.WaitForExit(5000);
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            output = string.Empty;
            error = ex.Message;
            return -1;
        }
    }
}
