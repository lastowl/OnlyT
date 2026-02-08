using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace OnlyT.Avalonia.Utils;

/// <summary>
/// Web utilities for downloading content
/// </summary>
public static class WebUtils
{
    private static readonly HttpClient _httpClient = new();

    static WebUtils()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OnlyT/2.0");
    }

    public static string? LoadWithUserAgent(string url)
    {
        try
        {
            var task = Task.Run(async () => await _httpClient.GetStringAsync(url));
            return task.Result;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
