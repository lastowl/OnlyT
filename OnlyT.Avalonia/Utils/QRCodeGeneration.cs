using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media.Imaging;
using QRCoder;

namespace OnlyT.Avalonia.Utils;

/// <summary>
/// QR code generation utilities
/// </summary>
internal static class QRCodeGeneration
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

    /// <summary>
    /// Creates a QR code bitmap for the given URL
    /// </summary>
    public static Bitmap? CreateQRCode(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Cache.TryGetValue(url, out var result))
        {
            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                using var code = new PngByteQRCode(data);
                var pngBytes = code.GetGraphic(20);

                using var stream = new MemoryStream(pngBytes);
                result = new Bitmap(stream);
                Cache.TryAdd(url, result);
            }
            catch (Exception)
            {
                return null;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the local web clock URL
    /// </summary>
    public static string GetWebClockUrl(int port)
    {
        var localIp = GetLocalIpAddress();
        return $"http://{localIp}:{port}/index/";
    }

    /// <summary>
    /// Gets the local IP address
    /// </summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
            // Ignore
        }

        return "localhost";
    }
}
