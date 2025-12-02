using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Service for translating text using free translation APIs
/// Uses LibreTranslate (free, open-source) with fallback to MyMemory
/// </summary>
public class FreeTranslationService : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    // Free LibreTranslate instances
    private static readonly string[] LibreTranslateServers =
    [
        "https://libretranslate.com",
        "https://translate.argosopentech.com",
        "https://translate.terraprint.co"
    ];

    // MyMemory is a free translation API with 5000 chars/day limit
    private const string MyMemoryApiUrl = "https://api.mymemory.translated.net/get";

    private int _currentServerIndex = 0;
    private int _requestCount = 0;
    private const int MaxRequestsPerServer = 50; // Switch servers periodically

    public FreeTranslationService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OnlyT-TranslationExtractor/1.0");
    }

    /// <summary>
    /// Translate text from English to the target language
    /// </summary>
    public async Task<string?> TranslateAsync(string text, string targetLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Try MyMemory first (more reliable)
        var result = await TryMyMemoryAsync(text, targetLanguageCode);
        if (result != null)
            return result;

        // Fallback to LibreTranslate
        result = await TryLibreTranslateAsync(text, targetLanguageCode);
        if (result != null)
            return result;

        Log.Debug("All translation services failed for '{Text}' to {Lang}", text, targetLanguageCode);
        return null;
    }

    /// <summary>
    /// Translate multiple texts in batch (more efficient)
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        Dictionary<string, string> texts,
        string targetLanguageCode)
    {
        var results = new Dictionary<string, string>();
        var delayMs = 200; // Rate limiting

        foreach (var kvp in texts)
        {
            try
            {
                var translated = await TranslateAsync(kvp.Value, targetLanguageCode);
                if (translated != null)
                {
                    results[kvp.Key] = translated;
                }

                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to translate key {Key}", kvp.Key);
            }
        }

        return results;
    }

    private async Task<string?> TryLibreTranslateAsync(string text, string targetLanguageCode)
    {
        // Convert culture code (e.g., "fr-FR") to language code (e.g., "fr")
        var langCode = GetLanguageCode(targetLanguageCode);

        for (int attempt = 0; attempt < LibreTranslateServers.Length; attempt++)
        {
            var serverUrl = LibreTranslateServers[_currentServerIndex];

            try
            {
                var requestBody = new
                {
                    q = text,
                    source = "en",
                    target = langCode,
                    format = "text"
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{serverUrl}/translate",
                    requestBody);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonDocument.Parse(json);

                    if (result.RootElement.TryGetProperty("translatedText", out var translatedText))
                    {
                        _requestCount++;

                        // Rotate servers periodically
                        if (_requestCount >= MaxRequestsPerServer)
                        {
                            _currentServerIndex = (_currentServerIndex + 1) % LibreTranslateServers.Length;
                            _requestCount = 0;
                        }

                        return translatedText.GetString();
                    }
                }
                else
                {
                    Log.Debug("LibreTranslate server {Server} returned {Status}",
                        serverUrl, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "LibreTranslate server {Server} failed", serverUrl);
            }

            // Try next server
            _currentServerIndex = (_currentServerIndex + 1) % LibreTranslateServers.Length;
        }

        return null;
    }

    private async Task<string?> TryMyMemoryAsync(string text, string targetLanguageCode)
    {
        var langCode = GetLanguageCode(targetLanguageCode);

        try
        {
            var url = $"{MyMemoryApiUrl}?q={Uri.EscapeDataString(text)}&langpair=en|{langCode}";
            var response = await _httpClient.GetStringAsync(url);

            var json = JsonDocument.Parse(response);

            if (json.RootElement.TryGetProperty("responseData", out var responseData) &&
                responseData.TryGetProperty("translatedText", out var translatedText))
            {
                var translated = translatedText.GetString();

                // MyMemory sometimes returns the input if translation fails
                if (translated != null && translated != text)
                {
                    return translated;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MyMemory translation failed");
        }

        return null;
    }

    private static string GetLanguageCode(string cultureCode)
    {
        // Convert "fr-FR" to "fr", "pt-BR" to "pt", etc.
        var parts = cultureCode.Split('-');
        return parts[0].ToLowerInvariant();
    }

    /// <summary>
    /// Check if a language is supported for translation
    /// </summary>
    public static bool IsLanguageSupported(string cultureCode)
    {
        var langCode = GetLanguageCode(cultureCode);

        // Common languages supported by most translation services
        var supportedLanguages = new HashSet<string>
        {
            "af", "sq", "am", "ar", "hy", "az", "eu", "be", "bn", "bs",
            "bg", "ca", "ceb", "zh", "co", "hr", "cs", "da", "nl", "en",
            "eo", "et", "fi", "fr", "fy", "gl", "ka", "de", "el", "gu",
            "ht", "ha", "haw", "he", "hi", "hmn", "hu", "is", "ig", "id",
            "ga", "it", "ja", "jv", "kn", "kk", "km", "rw", "ko", "ku",
            "ky", "lo", "la", "lv", "lt", "lb", "mk", "mg", "ms", "ml",
            "mt", "mi", "mr", "mn", "my", "ne", "no", "ny", "or", "ps",
            "fa", "pl", "pt", "pa", "ro", "ru", "sm", "gd", "sr", "st",
            "sn", "sd", "si", "sk", "sl", "so", "es", "su", "sw", "sv",
            "tl", "tg", "ta", "tt", "te", "th", "tr", "tk", "uk", "ur",
            "ug", "uz", "vi", "cy", "xh", "yi", "yo", "zu"
        };

        return supportedLanguages.Contains(langCode);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
