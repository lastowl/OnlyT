using System.Text.RegularExpressions;
using System.Xml.Linq;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Reads translations of the same strings from another set of resx files, such as the upstream
/// WPF OnlyT resources, which are maintained by human translators
/// </summary>
public class UpstreamTranslations
{
    // Cultures whose upstream file uses a different language code
    private static readonly Dictionary<string, string> CultureAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "nb", "no" },
    };

    private readonly string _directory;
    private readonly string _prefix;
    private readonly Dictionary<string, string> _english;
    private readonly List<string> _cultures;

    public UpstreamTranslations(string baseResxPath)
    {
        _directory = Path.GetDirectoryName(Path.GetFullPath(baseResxPath))!;
        _prefix = Path.GetFileNameWithoutExtension(baseResxPath) + ".";
        _english = ReadValues(baseResxPath);
        _cultures = Directory.GetFiles(_directory, _prefix + "*.resx")
            .Select(f => Path.GetFileNameWithoutExtension(f)[_prefix.Length..])
            .Where(c => c.Length > 0)
            .ToList();

        Log.Information("Loaded upstream translations for {Count} cultures from {Directory}", _cultures.Count, _directory);
    }

    /// <summary>
    /// Upstream translations for a culture, limited to keys whose English text is the same in both
    /// sets of resources and whose placeholders match
    /// </summary>
    public Dictionary<string, string> GetTranslations(string culture, IReadOnlyDictionary<string, string> baseValues)
    {
        var upstreamCulture = FindCulture(culture);
        if (upstreamCulture == null)
        {
            return new Dictionary<string, string>();
        }

        return ReadValues(Path.Combine(_directory, _prefix + upstreamCulture + ".resx"))
            .Where(kvp =>
                baseValues.TryGetValue(kvp.Key, out var english) &&
                _english.TryGetValue(kvp.Key, out var upstreamEnglish) &&
                english.Trim() == upstreamEnglish.Trim() &&
                !string.IsNullOrWhiteSpace(kvp.Value) &&
                kvp.Value != upstreamEnglish &&
                Placeholders(kvp.Value) == Placeholders(english))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private string? FindCulture(string culture)
    {
        culture = CultureAliases.GetValueOrDefault(culture, culture);
        var primary = culture.Split('-')[0];

        // The base resources are English, so English variants have nothing to take
        if (primary.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var samePrimary = _cultures
            .Where(c => c.Split('-')[0].Equals(primary, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Upstream keeps regional files (e.g. "sl-SI") more up to date than neutral ones ("sl")
        if (!culture.Contains('-'))
        {
            var regional = samePrimary.Where(c => c.Contains('-')).ToList();
            if (regional.Count == 1)
            {
                return regional[0];
            }
        }

        return _cultures.FirstOrDefault(c => c.Equals(culture, StringComparison.OrdinalIgnoreCase))
            ?? (samePrimary.Count == 1 ? samePrimary[0] : null);
    }

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"\{\d+[^}]*\}").Select(m => m.Value).Order());

    private static Dictionary<string, string> ReadValues(string resxPath) =>
        XDocument.Load(resxPath).Root?.Elements("data")
            .Where(d => d.Attribute("name") != null)
            .GroupBy(d => d.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.Last().Element("value")?.Value ?? string.Empty)
        ?? new Dictionary<string, string>();
}
