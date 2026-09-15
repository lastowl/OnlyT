using JwTranslationExtractor.Models;
using Newtonsoft.Json.Linq;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Maps .NET culture names used by OnlyT's resource files to jw.org languages, using
/// jw.org's language list (the data behind the site's language selector)
/// </summary>
public class JwLanguageCatalog
{
    private const string LanguagesUrl = "https://www.jw.org/en/languages/";

    // Cultures whose jw.org locale can't be derived from the culture name's primary language
    private static readonly Dictionary<string, string> LocaleOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        { "nb", "no" },
        { "zh-Hans", "cmn-hans" },
        { "zh-Hant", "cmn-hant" },
        { "zh-HK", "yue-hant" },
        { "ca-ES", "cat" },
        { "fil-PH", "tl" },
        { "tl-PH", "tl" },
        { "gn", "gug" },
        { "ku", "kmr-x-rd" },
    };

    private readonly List<JwLanguage> _languages;

    private JwLanguageCatalog(List<JwLanguage> languages)
    {
        _languages = languages;
    }

    public static async Task<JwLanguageCatalog> LoadAsync()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        var json = JObject.Parse(await httpClient.GetStringAsync(LanguagesUrl));

        var languages = (json["languages"] as JArray ?? new JArray())
            .Select(l => new JwLanguage
            {
                Symbol = (string?)l["symbol"] ?? string.Empty,
                LangCode = (string?)l["langcode"] ?? string.Empty,
                Name = (string?)l["name"] ?? string.Empty,
                VernacularName = (string?)l["vernacularName"] ?? string.Empty,
                Locale = ((string?)l["symbol"])?.ToLowerInvariant(),
                Script = (string?)l["script"],
                IsSignLanguage = (bool?)l["isSignLanguage"] ?? false,
                HasWebContent = (bool?)l["hasWebContent"] ?? false
            })
            .Where(l => l.IsSupported && !string.IsNullOrEmpty(l.Locale) && !string.IsNullOrEmpty(l.LangCode))
            .ToList();

        Log.Information("Loaded {Count} jw.org languages with web content", languages.Count);
        return new JwLanguageCatalog(languages);
    }

    /// <summary>
    /// Finds the jw.org language for a culture name (e.g. "de-DE" -> German, "X"). When a language
    /// has several script variants (e.g. Uzbek), the one matching <paramref name="script"/> is chosen.
    /// Returns null when there is no unambiguous match.
    /// </summary>
    public JwLanguage? Resolve(string cultureCode, string? script)
    {
        var locale = LocaleOverrides.TryGetValue(cultureCode, out var mapped) ? mapped : cultureCode.ToLowerInvariant();
        var primary = locale.Split('-')[0];

        var candidates = _languages
            .Where(l => l.Locale == locale || l.Locale!.Split('-')[0] == primary)
            .Where(l => script == null || ScriptDetector.FromJwScript(l.Script) == script)
            .ToList();

        return candidates.FirstOrDefault(l => l.Locale == locale)
            ?? (candidates.Count == 1 ? candidates[0] : candidates.FirstOrDefault(l => l.Locale == primary));
    }
}
