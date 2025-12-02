using System.Net.Http.Json;
using JwTranslationExtractor.Models;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Service for discovering languages available on jw.org
/// </summary>
public class LanguageDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
    private const string LanguagesApiUrl = "https://www.jw.org/en/languages/";

    // Mapping from JW language symbols/codes to .NET culture codes
    private static readonly Dictionary<string, string> JwToCultureMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Major languages
        { "E", "en" },         // English
        { "en", "en" },
        { "S", "es" },         // Spanish
        { "es", "es" },
        { "T", "pt" },         // Portuguese
        { "pt", "pt" },
        { "F", "fr" },         // French
        { "fr", "fr" },
        { "X", "de" },         // German
        { "de", "de" },
        { "I", "it" },         // Italian
        { "it", "it" },
        { "O", "ru" },         // Russian
        { "ru", "ru" },
        { "J", "ja" },         // Japanese
        { "ja", "ja" },
        { "K", "ko" },         // Korean
        { "ko", "ko" },

        // Chinese variants
        { "CH", "zh-Hans" },   // Chinese Traditional
        { "CHS", "zh-Hans" },  // Chinese Simplified
        { "CHT", "zh-Hant" },  // Chinese Traditional
        { "zh-hans", "zh-Hans" },
        { "zh-hant", "zh-Hant" },
        { "CHC", "zh-HK" },    // Chinese Cantonese
        { "yue-hant", "zh-HK" },

        // European languages
        { "U", "nl" },         // Dutch
        { "nl", "nl" },
        { "PL", "pl" },        // Polish
        { "pl", "pl" },
        { "G", "el" },         // Greek
        { "el", "el" },
        { "TK", "tr" },        // Turkish
        { "tr", "tr" },
        { "FI", "fi" },        // Finnish
        { "fi", "fi" },
        { "SV", "sv" },        // Swedish
        { "sv", "sv" },
        { "NO", "nb" },        // Norwegian Bokmal
        { "nb", "nb" },
        { "D", "da" },         // Danish
        { "da", "da" },
        { "B", "cs" },         // Czech
        { "cs", "cs" },
        { "SK", "sk" },        // Slovak
        { "sk", "sk" },
        { "H", "hu" },         // Hungarian
        { "hu", "hu" },
        { "RO", "ro" },        // Romanian
        { "ro", "ro" },
        { "BL", "bg" },        // Bulgarian
        { "bg", "bg" },
        { "SR", "sr" },        // Serbian
        { "sr", "sr" },
        { "C", "hr" },         // Croatian
        { "hr", "hr" },
        { "SL", "sl" },        // Slovenian
        { "sl", "sl" },
        { "UK", "uk" },        // Ukrainian
        { "uk", "uk" },
        { "ST", "et" },        // Estonian
        { "et", "et" },
        { "LV", "lv" },        // Latvian
        { "lv", "lv" },
        { "LT", "lt" },        // Lithuanian
        { "lt", "lt" },
        { "AL", "sq" },        // Albanian
        { "sq", "sq" },
        { "BSN", "bs" },       // Bosnian
        { "bs", "bs" },
        { "IB", "is" },        // Icelandic
        { "is", "is" },

        // Middle Eastern / RTL
        { "A", "ar" },         // Arabic
        { "ar", "ar" },
        { "AEY", "ar-EG" },    // Arabic (Egypt)
        { "Q", "he" },         // Hebrew
        { "he", "he" },
        { "FA", "fa" },        // Persian/Farsi
        { "fa", "fa" },
        { "KU", "ku" },        // Kurdish
        { "ku", "ku" },
        { "UR", "ur" },        // Urdu
        { "ur", "ur" },

        // South Asian
        { "HI", "hi" },        // Hindi
        { "hi", "hi" },
        { "BE", "bn" },        // Bengali
        { "bn", "bn" },
        { "TA", "ta" },        // Tamil
        { "ta", "ta" },
        { "TE", "te" },        // Telugu
        { "te", "te" },
        { "KN", "kn" },        // Kannada
        { "kn", "kn" },
        { "ML", "ml" },        // Malayalam
        { "ml", "ml" },
        { "GU", "gu" },        // Gujarati
        { "gu", "gu" },
        { "MR", "mr" },        // Marathi
        { "mr", "mr" },
        { "PA", "pa" },        // Punjabi
        { "pa", "pa" },
        { "NE", "ne" },        // Nepali
        { "ne", "ne" },
        { "SI", "si" },        // Sinhala
        { "si", "si" },
        { "AE", "as" },        // Assamese
        { "as", "as" },

        // Southeast Asian
        { "TH", "th" },        // Thai
        { "th", "th" },
        { "VI", "vi" },        // Vietnamese
        { "vi", "vi" },
        { "IN", "id" },        // Indonesian
        { "id", "id" },
        { "MS", "ms" },        // Malay
        { "ms", "ms" },
        { "TL", "fil" },       // Tagalog/Filipino
        { "fil", "fil" },
        { "tl", "fil" },
        { "CV", "ceb" },       // Cebuano
        { "ceb", "ceb" },
        { "IL", "ilo" },       // Ilocano
        { "ilo", "ilo" },
        { "KM", "km" },        // Khmer
        { "km", "km" },
        { "MY", "my" },        // Myanmar/Burmese
        { "my", "my" },
        { "LO", "lo" },        // Lao
        { "lo", "lo" },

        // African
        { "SW", "sw" },        // Swahili
        { "sw", "sw" },
        { "HA", "ha" },        // Hausa
        { "ha", "ha" },
        { "YO", "yo" },        // Yoruba
        { "yo", "yo" },
        { "IG", "ig" },        // Igbo
        { "ig", "ig" },
        { "AM", "am" },        // Amharic
        { "am", "am" },
        { "ZU", "zu" },        // Zulu
        { "zu", "zu" },
        { "XH", "xh" },        // Xhosa
        { "xh", "xh" },
        { "AF", "af" },        // Afrikaans
        { "af", "af" },
        { "LM", "bi" },        // Bislama
        { "bi", "bi" },

        // Central Asian / Caucasian
        { "GE", "ka" },        // Georgian
        { "ka", "ka" },
        { "AZ", "az" },        // Azerbaijani
        { "AJR", "az" },
        { "az", "az" },
        { "KZ", "kk" },        // Kazakh
        { "kk", "kk" },
        { "UZ", "uz" },        // Uzbek
        { "uz", "uz" },
        { "BAK", "ba" },       // Bashkir
        { "ba", "ba" },
        { "HY", "hy" },        // Armenian
        { "hy", "hy" },

        // Other European
        { "AN", "ca" },        // Catalan
        { "ca", "ca" },
        { "BQ", "eu" },        // Basque
        { "eu", "eu" },
        { "GL", "gl" },        // Galician
        { "gl", "gl" },
        { "MT", "mt" },        // Maltese
        { "mt", "mt" },
        { "GA", "ga" },        // Irish
        { "ga", "ga" },
        { "CY", "cy" },        // Welsh
        { "cy", "cy" },
    };

    public LanguageDiscoveryService()
    {
        // Use SocketsHttpHandler with specific settings for better compatibility
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            ResponseDrainTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        _httpClient.Timeout = TimeSpan.FromSeconds(120);

        // Configure retry with exponential backoff
        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(3),
                OnRetry = args =>
                {
                    Log.Warning("Retry {Attempt} for language discovery, waiting {Delay}s",
                        args.AttemptNumber, args.RetryDelay.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Fetches all available languages from jw.org
    /// </summary>
    public async Task<List<JwLanguage>> GetAllLanguagesAsync()
    {
        try
        {
            Log.Information("Fetching language list from jw.org...");

            // The languages endpoint returns HTML with embedded JSON
            var httpResponse = await _retryPipeline.ExecuteAsync(async ct =>
                await _httpClient.GetAsync(LanguagesApiUrl, ct));
            httpResponse.EnsureSuccessStatusCode();
            var response = await httpResponse.Content.ReadAsStringAsync();

            // Parse the languages from the page
            var languages = ParseLanguagesFromPage(response);

            // Map JW symbols to .NET culture codes
            foreach (var lang in languages)
            {
                if (JwToCultureMapping.TryGetValue(lang.Symbol.ToUpperInvariant(), out var cultureCode))
                {
                    lang.CultureCode = cultureCode;
                }
                else
                {
                    // Try to use the langcode as culture code
                    lang.CultureCode = lang.LangCode;
                }
            }

            Log.Information("Found {Count} languages ({Supported} supported, excluding sign languages)",
                languages.Count, languages.Count(l => l.IsSupported));

            return languages;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch languages from jw.org");
            throw;
        }
    }

    /// <summary>
    /// Gets only the supported languages (non-sign, with web content)
    /// </summary>
    public async Task<List<JwLanguage>> GetSupportedLanguagesAsync()
    {
        var allLanguages = await GetAllLanguagesAsync();
        return allLanguages.Where(l => l.IsSupported).ToList();
    }

    /// <summary>
    /// Gets a predefined list of common languages without requiring network access
    /// </summary>
    public List<JwLanguage> GetOfflineLanguages()
    {
        Log.Information("Using offline language list...");

        var languages = new List<JwLanguage>
        {
            // Major world languages
            new() { Symbol = "en", LangCode = "E", Name = "English", VernacularName = "English", CultureCode = "en", HasWebContent = true },
            new() { Symbol = "es", LangCode = "S", Name = "Spanish", VernacularName = "Español", CultureCode = "es", HasWebContent = true },
            new() { Symbol = "pt", LangCode = "T", Name = "Portuguese", VernacularName = "Português", CultureCode = "pt", HasWebContent = true },
            new() { Symbol = "pt-BR", LangCode = "TPO", Name = "Portuguese (Brazil)", VernacularName = "Português (Brasil)", CultureCode = "pt-BR", HasWebContent = true },
            new() { Symbol = "fr", LangCode = "F", Name = "French", VernacularName = "Français", CultureCode = "fr", HasWebContent = true },
            new() { Symbol = "de", LangCode = "X", Name = "German", VernacularName = "Deutsch", CultureCode = "de", HasWebContent = true },
            new() { Symbol = "it", LangCode = "I", Name = "Italian", VernacularName = "Italiano", CultureCode = "it", HasWebContent = true },
            new() { Symbol = "ru", LangCode = "U", Name = "Russian", VernacularName = "Русский", CultureCode = "ru", HasWebContent = true },
            new() { Symbol = "ja", LangCode = "J", Name = "Japanese", VernacularName = "日本語", CultureCode = "ja", HasWebContent = true },
            new() { Symbol = "ko", LangCode = "K", Name = "Korean", VernacularName = "한국어", CultureCode = "ko", HasWebContent = true },
            new() { Symbol = "zh-Hans", LangCode = "CHS", Name = "Chinese (Simplified)", VernacularName = "中文简体", CultureCode = "zh-Hans", HasWebContent = true },
            new() { Symbol = "zh-Hant", LangCode = "CH", Name = "Chinese (Traditional)", VernacularName = "中文繁體", CultureCode = "zh-Hant", HasWebContent = true },
            new() { Symbol = "ar", LangCode = "A", Name = "Arabic", VernacularName = "العربية", CultureCode = "ar", HasWebContent = true },
            new() { Symbol = "hi", LangCode = "HI", Name = "Hindi", VernacularName = "हिंदी", CultureCode = "hi", HasWebContent = true },
            new() { Symbol = "bn", LangCode = "BE", Name = "Bengali", VernacularName = "বাংলা", CultureCode = "bn", HasWebContent = true },

            // European languages
            new() { Symbol = "nl", LangCode = "O", Name = "Dutch", VernacularName = "Nederlands", CultureCode = "nl", HasWebContent = true },
            new() { Symbol = "pl", LangCode = "P", Name = "Polish", VernacularName = "Polski", CultureCode = "pl", HasWebContent = true },
            new() { Symbol = "el", LangCode = "G", Name = "Greek", VernacularName = "Ελληνική", CultureCode = "el", HasWebContent = true },
            new() { Symbol = "tr", LangCode = "TK", Name = "Turkish", VernacularName = "Türkçe", CultureCode = "tr", HasWebContent = true },
            new() { Symbol = "sv", LangCode = "Z", Name = "Swedish", VernacularName = "Svenska", CultureCode = "sv", HasWebContent = true },
            new() { Symbol = "nb", LangCode = "N", Name = "Norwegian", VernacularName = "Norsk", CultureCode = "nb", HasWebContent = true },
            new() { Symbol = "da", LangCode = "D", Name = "Danish", VernacularName = "Dansk", CultureCode = "da", HasWebContent = true },
            new() { Symbol = "fi", LangCode = "M", Name = "Finnish", VernacularName = "Suomi", CultureCode = "fi", HasWebContent = true },
            new() { Symbol = "cs", LangCode = "B", Name = "Czech", VernacularName = "Čeština", CultureCode = "cs", HasWebContent = true },
            new() { Symbol = "sk", LangCode = "V", Name = "Slovak", VernacularName = "Slovenčina", CultureCode = "sk", HasWebContent = true },
            new() { Symbol = "hu", LangCode = "H", Name = "Hungarian", VernacularName = "Magyar", CultureCode = "hu", HasWebContent = true },
            new() { Symbol = "ro", LangCode = "R", Name = "Romanian", VernacularName = "Română", CultureCode = "ro", HasWebContent = true },
            new() { Symbol = "bg", LangCode = "BL", Name = "Bulgarian", VernacularName = "Български", CultureCode = "bg", HasWebContent = true },
            new() { Symbol = "sr", LangCode = "SB", Name = "Serbian", VernacularName = "Srpski", CultureCode = "sr", HasWebContent = true },
            new() { Symbol = "hr", LangCode = "C", Name = "Croatian", VernacularName = "Hrvatski", CultureCode = "hr", HasWebContent = true },
            new() { Symbol = "sl", LangCode = "SV", Name = "Slovenian", VernacularName = "Slovenščina", CultureCode = "sl", HasWebContent = true },
            new() { Symbol = "uk", LangCode = "K", Name = "Ukrainian", VernacularName = "Українська", CultureCode = "uk", HasWebContent = true },
            new() { Symbol = "et", LangCode = "ST", Name = "Estonian", VernacularName = "Eesti", CultureCode = "et", HasWebContent = true },
            new() { Symbol = "lv", LangCode = "LT", Name = "Latvian", VernacularName = "Latviešu", CultureCode = "lv", HasWebContent = true },
            new() { Symbol = "lt", LangCode = "L", Name = "Lithuanian", VernacularName = "Lietuvių", CultureCode = "lt", HasWebContent = true },
            new() { Symbol = "sq", LangCode = "AL", Name = "Albanian", VernacularName = "Shqip", CultureCode = "sq", HasWebContent = true },
            new() { Symbol = "mk", LangCode = "MA", Name = "Macedonian", VernacularName = "Македонски", CultureCode = "mk", HasWebContent = true },
            new() { Symbol = "bs", LangCode = "BS", Name = "Bosnian", VernacularName = "Bosanski", CultureCode = "bs", HasWebContent = true },
            new() { Symbol = "is", LangCode = "IC", Name = "Icelandic", VernacularName = "Íslenska", CultureCode = "is", HasWebContent = true },
            new() { Symbol = "ca", LangCode = "CT", Name = "Catalan", VernacularName = "Català", CultureCode = "ca", HasWebContent = true },
            new() { Symbol = "eu", LangCode = "BQ", Name = "Basque", VernacularName = "Euskara", CultureCode = "eu", HasWebContent = true },
            new() { Symbol = "gl", LangCode = "GC", Name = "Galician", VernacularName = "Galego", CultureCode = "gl", HasWebContent = true },
            new() { Symbol = "mt", LangCode = "ML", Name = "Maltese", VernacularName = "Malti", CultureCode = "mt", HasWebContent = true },
            new() { Symbol = "ga", LangCode = "GI", Name = "Irish", VernacularName = "Gaeilge", CultureCode = "ga", HasWebContent = true },
            new() { Symbol = "cy", LangCode = "WE", Name = "Welsh", VernacularName = "Cymraeg", CultureCode = "cy", HasWebContent = true },
            new() { Symbol = "be", LangCode = "BY", Name = "Belarusian", VernacularName = "Беларуская", CultureCode = "be", HasWebContent = true },

            // Middle East / RTL
            new() { Symbol = "he", LangCode = "Q", Name = "Hebrew", VernacularName = "עברית", CultureCode = "he", HasWebContent = true },
            new() { Symbol = "fa", LangCode = "PE", Name = "Persian", VernacularName = "فارسی", CultureCode = "fa", HasWebContent = true },
            new() { Symbol = "ur", LangCode = "UR", Name = "Urdu", VernacularName = "اردو", CultureCode = "ur", HasWebContent = true },
            new() { Symbol = "ku", LangCode = "KU", Name = "Kurdish", VernacularName = "کوردی", CultureCode = "ku", HasWebContent = true },

            // South Asian
            new() { Symbol = "ta", LangCode = "TA", Name = "Tamil", VernacularName = "தமிழ்", CultureCode = "ta", HasWebContent = true },
            new() { Symbol = "te", LangCode = "TE", Name = "Telugu", VernacularName = "తెలుగు", CultureCode = "te", HasWebContent = true },
            new() { Symbol = "kn", LangCode = "KA", Name = "Kannada", VernacularName = "ಕನ್ನಡ", CultureCode = "kn", HasWebContent = true },
            new() { Symbol = "ml", LangCode = "MY", Name = "Malayalam", VernacularName = "മലയാളം", CultureCode = "ml", HasWebContent = true },
            new() { Symbol = "gu", LangCode = "GJ", Name = "Gujarati", VernacularName = "ગુજરાતી", CultureCode = "gu", HasWebContent = true },
            new() { Symbol = "mr", LangCode = "MR", Name = "Marathi", VernacularName = "मराठी", CultureCode = "mr", HasWebContent = true },
            new() { Symbol = "pa", LangCode = "PJ", Name = "Punjabi", VernacularName = "ਪੰਜਾਬੀ", CultureCode = "pa", HasWebContent = true },
            new() { Symbol = "ne", LangCode = "NP", Name = "Nepali", VernacularName = "नेपाली", CultureCode = "ne", HasWebContent = true },
            new() { Symbol = "si", LangCode = "SG", Name = "Sinhala", VernacularName = "සිංහල", CultureCode = "si", HasWebContent = true },
            new() { Symbol = "as", LangCode = "AE", Name = "Assamese", VernacularName = "অসমীয়া", CultureCode = "as", HasWebContent = true },
            new() { Symbol = "or", LangCode = "OD", Name = "Odia", VernacularName = "ଓଡ଼ିଆ", CultureCode = "or", HasWebContent = true },

            // Southeast Asian
            new() { Symbol = "th", LangCode = "SI", Name = "Thai", VernacularName = "ไทย", CultureCode = "th", HasWebContent = true },
            new() { Symbol = "vi", LangCode = "VT", Name = "Vietnamese", VernacularName = "Tiếng Việt", CultureCode = "vi", HasWebContent = true },
            new() { Symbol = "id", LangCode = "IN", Name = "Indonesian", VernacularName = "Indonesia", CultureCode = "id", HasWebContent = true },
            new() { Symbol = "ms", LangCode = "MS", Name = "Malay", VernacularName = "Bahasa Melayu", CultureCode = "ms", HasWebContent = true },
            new() { Symbol = "fil", LangCode = "TG", Name = "Filipino", VernacularName = "Filipino", CultureCode = "fil", HasWebContent = true },
            new() { Symbol = "ceb", LangCode = "CV", Name = "Cebuano", VernacularName = "Cebuano", CultureCode = "ceb", HasWebContent = true },
            new() { Symbol = "ilo", LangCode = "IL", Name = "Ilocano", VernacularName = "Ilocano", CultureCode = "ilo", HasWebContent = true },
            new() { Symbol = "km", LangCode = "CB", Name = "Khmer", VernacularName = "ខ្មែរ", CultureCode = "km", HasWebContent = true },
            new() { Symbol = "my", LangCode = "MY", Name = "Myanmar", VernacularName = "မြန်မာ", CultureCode = "my", HasWebContent = true },
            new() { Symbol = "lo", LangCode = "LA", Name = "Lao", VernacularName = "ລາວ", CultureCode = "lo", HasWebContent = true },

            // African languages
            new() { Symbol = "sw", LangCode = "SW", Name = "Swahili", VernacularName = "Kiswahili", CultureCode = "sw", HasWebContent = true },
            new() { Symbol = "ha", LangCode = "HA", Name = "Hausa", VernacularName = "Hausa", CultureCode = "ha", HasWebContent = true },
            new() { Symbol = "yo", LangCode = "YO", Name = "Yoruba", VernacularName = "Yorùbá", CultureCode = "yo", HasWebContent = true },
            new() { Symbol = "ig", LangCode = "IB", Name = "Igbo", VernacularName = "Igbo", CultureCode = "ig", HasWebContent = true },
            new() { Symbol = "am", LangCode = "AM", Name = "Amharic", VernacularName = "አማርኛ", CultureCode = "am", HasWebContent = true },
            new() { Symbol = "zu", LangCode = "ZU", Name = "Zulu", VernacularName = "IsiZulu", CultureCode = "zu", HasWebContent = true },
            new() { Symbol = "xh", LangCode = "XO", Name = "Xhosa", VernacularName = "IsiXhosa", CultureCode = "xh", HasWebContent = true },
            new() { Symbol = "af", LangCode = "AF", Name = "Afrikaans", VernacularName = "Afrikaans", CultureCode = "af", HasWebContent = true },
            new() { Symbol = "so", LangCode = "SM", Name = "Somali", VernacularName = "Soomaali", CultureCode = "so", HasWebContent = true },
            new() { Symbol = "rw", LangCode = "KI", Name = "Kinyarwanda", VernacularName = "Ikinyarwanda", CultureCode = "rw", HasWebContent = true },
            new() { Symbol = "sn", LangCode = "SH", Name = "Shona", VernacularName = "ChiShona", CultureCode = "sn", HasWebContent = true },
            new() { Symbol = "mg", LangCode = "MG", Name = "Malagasy", VernacularName = "Malagasy", CultureCode = "mg", HasWebContent = true },
            new() { Symbol = "ny", LangCode = "NY", Name = "Chichewa", VernacularName = "Chichewa", CultureCode = "ny", HasWebContent = true },
            new() { Symbol = "lg", LangCode = "LG", Name = "Luganda", VernacularName = "Luganda", CultureCode = "lg", HasWebContent = true },
            new() { Symbol = "ti", LangCode = "TI", Name = "Tigrinya", VernacularName = "ትግርኛ", CultureCode = "ti", HasWebContent = true },
            new() { Symbol = "wo", LangCode = "WO", Name = "Wolof", VernacularName = "Wolof", CultureCode = "wo", HasWebContent = true },
            new() { Symbol = "ln", LangCode = "LI", Name = "Lingala", VernacularName = "Lingála", CultureCode = "ln", HasWebContent = true },
            new() { Symbol = "ts", LangCode = "TS", Name = "Tsonga", VernacularName = "Xitsonga", CultureCode = "ts", HasWebContent = true },
            new() { Symbol = "st", LangCode = "SE", Name = "Sesotho", VernacularName = "Sesotho", CultureCode = "st", HasWebContent = true },
            new() { Symbol = "tn", LangCode = "TN", Name = "Setswana", VernacularName = "Setswana", CultureCode = "tn", HasWebContent = true },
            new() { Symbol = "nso", LangCode = "NS", Name = "Northern Sotho", VernacularName = "Sepedi", CultureCode = "nso", HasWebContent = true },
            new() { Symbol = "ve", LangCode = "VE", Name = "Venda", VernacularName = "Tshivenda", CultureCode = "ve", HasWebContent = true },
            new() { Symbol = "ss", LangCode = "SS", Name = "Swati", VernacularName = "SiSwati", CultureCode = "ss", HasWebContent = true },
            new() { Symbol = "nr", LangCode = "ND", Name = "Southern Ndebele", VernacularName = "IsiNdebele", CultureCode = "nr", HasWebContent = true },

            // Central Asian / Caucasian
            new() { Symbol = "ka", LangCode = "GR", Name = "Georgian", VernacularName = "ქართული", CultureCode = "ka", HasWebContent = true },
            new() { Symbol = "az", LangCode = "AZ", Name = "Azerbaijani", VernacularName = "Azərbaycan", CultureCode = "az", HasWebContent = true },
            new() { Symbol = "kk", LangCode = "KZ", Name = "Kazakh", VernacularName = "Қазақ", CultureCode = "kk", HasWebContent = true },
            new() { Symbol = "uz", LangCode = "UB", Name = "Uzbek", VernacularName = "Oʻzbek", CultureCode = "uz", HasWebContent = true },
            new() { Symbol = "ky", LangCode = "KY", Name = "Kyrgyz", VernacularName = "Кыргызча", CultureCode = "ky", HasWebContent = true },
            new() { Symbol = "tg", LangCode = "TJ", Name = "Tajik", VernacularName = "Тоҷикӣ", CultureCode = "tg", HasWebContent = true },
            new() { Symbol = "tk", LangCode = "TM", Name = "Turkmen", VernacularName = "Türkmen", CultureCode = "tk", HasWebContent = true },
            new() { Symbol = "hy", LangCode = "HY", Name = "Armenian", VernacularName = "Հայերեdelays", CultureCode = "hy", HasWebContent = true },
            new() { Symbol = "mn", LangCode = "MN", Name = "Mongolian", VernacularName = "Монгол", CultureCode = "mn", HasWebContent = true },

            // Pacific languages
            new() { Symbol = "bi", LangCode = "BI", Name = "Bislama", VernacularName = "Bislama", CultureCode = "bi", HasWebContent = true },
            new() { Symbol = "to", LangCode = "TO", Name = "Tongan", VernacularName = "Lea fakatonga", CultureCode = "to", HasWebContent = true },
            new() { Symbol = "sm", LangCode = "SA", Name = "Samoan", VernacularName = "Gagana Samoa", CultureCode = "sm", HasWebContent = true },
            new() { Symbol = "fj", LangCode = "FJ", Name = "Fijian", VernacularName = "Vosa Vakaviti", CultureCode = "fj", HasWebContent = true },
            new() { Symbol = "mi", LangCode = "MO", Name = "Maori", VernacularName = "Te reo Māori", CultureCode = "mi", HasWebContent = true },
            new() { Symbol = "haw", LangCode = "HW", Name = "Hawaiian", VernacularName = "ʻŌlelo Hawaiʻi", CultureCode = "haw", HasWebContent = true },
            new() { Symbol = "tpi", LangCode = "TP", Name = "Tok Pisin", VernacularName = "Tok Pisin", CultureCode = "tpi", HasWebContent = true },

            // Caribbean / Americas
            new() { Symbol = "ht", LangCode = "CR", Name = "Haitian Creole", VernacularName = "Kreyòl ayisyen", CultureCode = "ht", HasWebContent = true },
            new() { Symbol = "pap", LangCode = "PP", Name = "Papiamento", VernacularName = "Papiamentu", CultureCode = "pap", HasWebContent = true },
            new() { Symbol = "srn", LangCode = "SR", Name = "Sranan Tongo", VernacularName = "Sranan", CultureCode = "srn", HasWebContent = true },
            new() { Symbol = "gn", LangCode = "GN", Name = "Guarani", VernacularName = "Avañe'ẽ", CultureCode = "gn", HasWebContent = true },
            new() { Symbol = "qu", LangCode = "QU", Name = "Quechua", VernacularName = "Runasimi", CultureCode = "qu", HasWebContent = true },
            new() { Symbol = "ay", LangCode = "AY", Name = "Aymara", VernacularName = "Aymar aru", CultureCode = "ay", HasWebContent = true },

            // Regional variants
            new() { Symbol = "en-US", LangCode = "US", Name = "English (US)", VernacularName = "English (US)", CultureCode = "en-US", HasWebContent = true },
            new() { Symbol = "en-GB", LangCode = "UK", Name = "English (UK)", VernacularName = "English (UK)", CultureCode = "en-GB", HasWebContent = true },
            new() { Symbol = "es-MX", LangCode = "MX", Name = "Spanish (Mexico)", VernacularName = "Español (México)", CultureCode = "es-MX", HasWebContent = true },
            new() { Symbol = "fr-CA", LangCode = "FC", Name = "French (Canada)", VernacularName = "Français (Canada)", CultureCode = "fr-CA", HasWebContent = true },
            new() { Symbol = "zh-HK", LangCode = "CHC", Name = "Chinese (Hong Kong)", VernacularName = "廣東話", CultureCode = "zh-HK", HasWebContent = true },
            new() { Symbol = "ar-EG", LangCode = "AEG", Name = "Arabic (Egypt)", VernacularName = "العربية (مصر)", CultureCode = "ar-EG", HasWebContent = true },

            // Additional European
            new() { Symbol = "eo", LangCode = "EO", Name = "Esperanto", VernacularName = "Esperanto", CultureCode = "eo", HasWebContent = true },
            new() { Symbol = "lb", LangCode = "LX", Name = "Luxembourgish", VernacularName = "Lëtzebuergesch", CultureCode = "lb", HasWebContent = true },
            new() { Symbol = "rm", LangCode = "RM", Name = "Romansh", VernacularName = "Rumantsch", CultureCode = "rm", HasWebContent = true },
            new() { Symbol = "fy", LangCode = "FY", Name = "Frisian", VernacularName = "Frysk", CultureCode = "fy", HasWebContent = true },
            new() { Symbol = "kl", LangCode = "KL", Name = "Greenlandic", VernacularName = "Kalaallisut", CultureCode = "kl", HasWebContent = true },
            new() { Symbol = "fo", LangCode = "FO", Name = "Faroese", VernacularName = "Føroyskt", CultureCode = "fo", HasWebContent = true },

            // Additional Asian
            new() { Symbol = "bo", LangCode = "TB", Name = "Tibetan", VernacularName = "བོད་སྐད", CultureCode = "bo", HasWebContent = true },
            new() { Symbol = "dz", LangCode = "DZ", Name = "Dzongkha", VernacularName = "རྫོང་ཁ", CultureCode = "dz", HasWebContent = true },
            new() { Symbol = "mni", LangCode = "MN", Name = "Manipuri", VernacularName = "মৈতৈলোন্", CultureCode = "mni", HasWebContent = true },
            new() { Symbol = "doi", LangCode = "DG", Name = "Dogri", VernacularName = "डोगरी", CultureCode = "doi", HasWebContent = true },
            new() { Symbol = "sat", LangCode = "SAT", Name = "Santali", VernacularName = "ᱥᱟᱱᱛᱟᱲᱤ", CultureCode = "sat", HasWebContent = true },
            new() { Symbol = "mai", LangCode = "MT", Name = "Maithili", VernacularName = "मैथिली", CultureCode = "mai", HasWebContent = true },
            new() { Symbol = "ks", LangCode = "KS", Name = "Kashmiri", VernacularName = "کٲشُر", CultureCode = "ks", HasWebContent = true },
            new() { Symbol = "sd", LangCode = "SD", Name = "Sindhi", VernacularName = "سنڌي", CultureCode = "sd", HasWebContent = true },
            new() { Symbol = "kok", LangCode = "KK", Name = "Konkani", VernacularName = "कोंकणी", CultureCode = "kok", HasWebContent = true },
        };

        Log.Information("Loaded {Count} languages from offline list", languages.Count);
        return languages;
    }

    private List<JwLanguage> ParseLanguagesFromPage(string html)
    {
        var languages = new List<JwLanguage>();

        // Look for the JSON data in the page
        // The page typically contains a script with language data
        var startMarker = "\"languages\":";
        var startIndex = html.IndexOf(startMarker);

        if (startIndex == -1)
        {
            // Try alternative approach - parse from select options or links
            return ParseLanguagesFromHtml(html);
        }

        try
        {
            // Find the JSON array
            startIndex += startMarker.Length;
            var bracketCount = 0;
            var jsonStart = -1;
            var jsonEnd = -1;

            for (int i = startIndex; i < html.Length; i++)
            {
                if (html[i] == '[')
                {
                    if (jsonStart == -1) jsonStart = i;
                    bracketCount++;
                }
                else if (html[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        jsonEnd = i + 1;
                        break;
                    }
                }
            }

            if (jsonStart != -1 && jsonEnd != -1)
            {
                var json = html.Substring(jsonStart, jsonEnd - jsonStart);
                var languageData = JsonConvert.DeserializeObject<List<JwLanguageData>>(json);

                if (languageData != null)
                {
                    languages = languageData.Select(ld => new JwLanguage
                    {
                        Symbol = ld.Symbol ?? string.Empty,
                        LangCode = ld.LangCode ?? ld.Code ?? string.Empty,
                        Name = ld.Name ?? string.Empty,
                        VernacularName = ld.VernacularName ?? ld.VernName ?? string.Empty,
                        IsSignLanguage = ld.IsSignLanguage ?? false,
                        HasWebContent = ld.HasWebContent ?? true
                    }).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse JSON language data, falling back to HTML parsing");
            return ParseLanguagesFromHtml(html);
        }

        return languages;
    }

    private List<JwLanguage> ParseLanguagesFromHtml(string html)
    {
        var languages = new List<JwLanguage>();

        // Parse using HtmlAgilityPack
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Look for language links
        var langLinks = doc.DocumentNode.SelectNodes("//a[@data-lang]");
        if (langLinks != null)
        {
            foreach (var link in langLinks)
            {
                var symbol = link.GetAttributeValue("data-lang", "");
                var langCode = link.GetAttributeValue("data-langcode", "");
                var name = link.InnerText.Trim();

                if (!string.IsNullOrEmpty(symbol))
                {
                    languages.Add(new JwLanguage
                    {
                        Symbol = symbol,
                        LangCode = langCode,
                        Name = name,
                        VernacularName = name,
                        HasWebContent = true
                    });
                }
            }
        }

        return languages;
    }

    // Internal class for JSON deserialization
    private class JwLanguageData
    {
        [JsonProperty("symbol")]
        public string? Symbol { get; set; }

        [JsonProperty("langcode")]
        public string? LangCode { get; set; }

        [JsonProperty("code")]
        public string? Code { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("vernacularName")]
        public string? VernacularName { get; set; }

        [JsonProperty("vernName")]
        public string? VernName { get; set; }

        [JsonProperty("isSignLanguage")]
        public bool? IsSignLanguage { get; set; }

        [JsonProperty("hasWebContent")]
        public bool? HasWebContent { get; set; }
    }
}
