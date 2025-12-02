using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Google Cloud Translation API service
/// Requires an API key from Google Cloud Console
/// </summary>
public class GoogleTranslateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private bool _disposed;

    private const string ApiUrl = "https://translation.googleapis.com/language/translate/v2";

    public GoogleTranslateService(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Google API key is required", nameof(apiKey));

        _apiKey = apiKey;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Translate text from English to the target language
    /// </summary>
    public async Task<string?> TranslateAsync(string text, string targetLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        try
        {
            var langCode = GetLanguageCode(targetLanguageCode);

            var requestBody = new
            {
                q = text,
                source = "en",
                target = langCode,
                format = "text"
            };

            var url = $"{ApiUrl}?key={_apiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);

                if (result.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("translations", out var translations) &&
                    translations.GetArrayLength() > 0)
                {
                    var translation = translations[0];
                    if (translation.TryGetProperty("translatedText", out var translatedText))
                    {
                        return translatedText.GetString();
                    }
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Log.Warning("Google Translate API error: {Status} - {Error}",
                    response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Google Translate failed for '{Text}' to {Lang}", text, targetLanguageCode);
        }

        return null;
    }

    /// <summary>
    /// Translate multiple texts in a single API call (more efficient)
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        Dictionary<string, string> texts,
        string targetLanguageCode)
    {
        var results = new Dictionary<string, string>();

        if (texts.Count == 0)
            return results;

        try
        {
            var langCode = GetLanguageCode(targetLanguageCode);
            var keys = texts.Keys.ToList();
            var values = texts.Values.ToList();

            // Google API supports up to 128 texts per request
            const int batchSize = 100;

            for (int i = 0; i < values.Count; i += batchSize)
            {
                var batchKeys = keys.Skip(i).Take(batchSize).ToList();
                var batchValues = values.Skip(i).Take(batchSize).ToList();

                var translations = await TranslateBatchInternalAsync(batchValues, langCode);

                if (translations != null && translations.Count == batchKeys.Count)
                {
                    for (int j = 0; j < batchKeys.Count; j++)
                    {
                        if (!string.IsNullOrEmpty(translations[j]))
                        {
                            results[batchKeys[j]] = translations[j]!;
                        }
                    }
                }

                // Small delay between batches to be nice to the API
                if (i + batchSize < values.Count)
                {
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Google Translate batch failed for {Lang}", targetLanguageCode);
        }

        return results;
    }

    private async Task<List<string?>?> TranslateBatchInternalAsync(List<string> texts, string targetLang)
    {
        try
        {
            // Build query string with multiple q parameters
            var queryParams = new StringBuilder($"key={_apiKey}&target={targetLang}&source=en&format=text");
            foreach (var text in texts)
            {
                queryParams.Append($"&q={Uri.EscapeDataString(text)}");
            }

            var url = $"{ApiUrl}?{queryParams}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(json);

                if (result.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("translations", out var translations))
                {
                    var translatedTexts = new List<string?>();
                    foreach (var translation in translations.EnumerateArray())
                    {
                        if (translation.TryGetProperty("translatedText", out var translatedText))
                        {
                            translatedTexts.Add(translatedText.GetString());
                        }
                        else
                        {
                            translatedTexts.Add(null);
                        }
                    }
                    return translatedTexts;
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Log.Warning("Google Translate batch API error: {Status} - {Error}",
                    response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Google Translate batch internal failed");
        }

        return null;
    }

    private static string GetLanguageCode(string cultureCode)
    {
        // Convert "fr-FR" to "fr", "pt-BR" to "pt", etc.
        // But keep special cases like "zh-CN" and "zh-TW"
        if (cultureCode.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
        {
            return cultureCode.ToLowerInvariant(); // Keep zh-CN, zh-TW
        }

        var parts = cultureCode.Split('-');
        return parts[0].ToLowerInvariant();
    }

    /// <summary>
    /// Check if a language is supported by Google Translate
    /// </summary>
    public static bool IsLanguageSupported(string cultureCode)
    {
        var langCode = GetLanguageCode(cultureCode);

        // Google supports 130+ languages - these are the most common
        var supportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "af", "sq", "am", "ar", "hy", "as", "ay", "az", "bm", "eu",
            "be", "bn", "bho", "bs", "bg", "ca", "ceb", "zh", "zh-cn", "zh-tw",
            "co", "hr", "cs", "da", "dv", "doi", "nl", "en", "eo", "et",
            "ee", "fil", "fi", "fr", "fy", "gl", "ka", "de", "el", "gn",
            "gu", "ht", "ha", "haw", "he", "hi", "hmn", "hu", "is", "ig",
            "ilo", "id", "ga", "it", "ja", "jv", "kn", "kk", "km", "rw",
            "gom", "ko", "kri", "ku", "ckb", "ky", "lo", "la", "lv", "ln",
            "lt", "lg", "lb", "mk", "mai", "mg", "ms", "ml", "mt", "mi",
            "mr", "mni", "lus", "mn", "my", "ne", "no", "ny", "or", "om",
            "ps", "fa", "pl", "pt", "pa", "qu", "ro", "ru", "sm", "sa",
            "gd", "nso", "sr", "st", "sn", "sd", "si", "sk", "sl", "so",
            "es", "su", "sw", "sv", "tl", "tg", "ta", "tt", "te", "th",
            "ti", "ts", "tr", "tk", "ak", "uk", "ur", "ug", "uz", "vi",
            "cy", "xh", "yi", "yo", "zu"
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
