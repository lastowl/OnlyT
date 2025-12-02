using System.Text.Json;
using System.Xml.Linq;
using JwTranslationExtractor.Models;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Service for tracking translation sources and managing the translation hierarchy
/// </summary>
public class TranslationTrackingService
{
    private readonly string _trackingDirectory;
    private readonly Dictionary<string, LanguageTranslationData> _trackingData = new();

    public TranslationTrackingService(string trackingDirectory)
    {
        _trackingDirectory = trackingDirectory;
        Directory.CreateDirectory(_trackingDirectory);
    }

    /// <summary>
    /// Load tracking data for a language
    /// </summary>
    public LanguageTranslationData LoadTrackingData(string cultureCode)
    {
        if (_trackingData.TryGetValue(cultureCode, out var cached))
        {
            return cached;
        }

        var trackingFile = Path.Combine(_trackingDirectory, $"{cultureCode}.tracking.json");

        if (File.Exists(trackingFile))
        {
            try
            {
                var json = File.ReadAllText(trackingFile);
                var data = JsonSerializer.Deserialize<LanguageTranslationData>(json);
                if (data != null)
                {
                    _trackingData[cultureCode] = data;
                    return data;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load tracking data for {Culture}", cultureCode);
            }
        }

        // Create new tracking data
        var newData = new LanguageTranslationData { CultureCode = cultureCode };
        _trackingData[cultureCode] = newData;
        return newData;
    }

    /// <summary>
    /// Save tracking data for a language
    /// </summary>
    public void SaveTrackingData(string cultureCode)
    {
        if (!_trackingData.TryGetValue(cultureCode, out var data))
        {
            return;
        }

        var trackingFile = Path.Combine(_trackingDirectory, $"{cultureCode}.tracking.json");

        try
        {
            data.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(trackingFile, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save tracking data for {Culture}", cultureCode);
        }
    }

    /// <summary>
    /// Read existing translations from a resx file and detect their source
    /// </summary>
    public Dictionary<string, TrackedTranslation> ReadExistingTranslations(
        string resxPath,
        string baseResxPath,
        string cultureCode)
    {
        var result = new Dictionary<string, TrackedTranslation>();

        if (!File.Exists(resxPath))
        {
            return result;
        }

        try
        {
            // Load base English values for comparison
            var baseValues = LoadBaseValues(baseResxPath);

            // Load existing resx
            var doc = XDocument.Load(resxPath);
            var root = doc.Root;
            if (root == null) return result;

            // Load tracking data
            var trackingData = LoadTrackingData(cultureCode);

            foreach (var data in root.Elements("data"))
            {
                var key = data.Attribute("name")?.Value;
                var valueElement = data.Element("value");
                var value = valueElement?.Value;

                if (string.IsNullOrEmpty(key) || value == null)
                    continue;

                // Determine source
                TranslationSource source;

                if (trackingData.Translations.TryGetValue(key, out var tracked))
                {
                    // We have tracking info - use it
                    source = tracked.Source;
                }
                else if (baseValues.TryGetValue(key, out var baseValue) && value == baseValue)
                {
                    // Same as English - it's a fallback
                    source = TranslationSource.Fallback;
                }
                else
                {
                    // Has a different value but no tracking - assume manual
                    source = TranslationSource.Manual;
                }

                result[key] = new TrackedTranslation
                {
                    Key = key,
                    Value = value,
                    Source = source,
                    LastUpdated = tracked?.LastUpdated ?? DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read existing translations from {Path}", resxPath);
        }

        return result;
    }

    /// <summary>
    /// Merge translations according to the priority hierarchy:
    /// JwOrg > Manual > ThirdParty > Fallback
    /// </summary>
    public Dictionary<string, TrackedTranslation> MergeTranslations(
        Dictionary<string, TrackedTranslation> existing,
        Dictionary<string, string>? jwOrgTranslations,
        Dictionary<string, string>? thirdPartyTranslations,
        Dictionary<string, string> baseEnglish)
    {
        var result = new Dictionary<string, TrackedTranslation>();

        // Start with all keys from base English
        foreach (var kvp in baseEnglish)
        {
            result[kvp.Key] = new TrackedTranslation
            {
                Key = kvp.Key,
                Value = kvp.Value,
                Source = TranslationSource.Fallback
            };
        }

        // Apply third-party translations (only if not already present from a better source)
        if (thirdPartyTranslations != null)
        {
            foreach (var kvp in thirdPartyTranslations)
            {
                if (!result.TryGetValue(kvp.Key, out var current) ||
                    current.Source < TranslationSource.ThirdParty)
                {
                    // Check if we already have a third-party translation that shouldn't be updated
                    if (existing.TryGetValue(kvp.Key, out var existingTranslation) &&
                        existingTranslation.Source == TranslationSource.ThirdParty)
                    {
                        // Keep the existing third-party translation (don't update)
                        result[kvp.Key] = existingTranslation;
                    }
                    else if (!existing.TryGetValue(kvp.Key, out _) ||
                             existing[kvp.Key].Source == TranslationSource.Fallback)
                    {
                        // Only apply third-party if we don't have it or only have fallback
                        result[kvp.Key] = new TrackedTranslation
                        {
                            Key = kvp.Key,
                            Value = kvp.Value,
                            Source = TranslationSource.ThirdParty
                        };
                    }
                }
            }
        }

        // Apply existing manual translations (overwrites third-party and fallback)
        foreach (var kvp in existing.Where(e => e.Value.Source == TranslationSource.Manual))
        {
            if (!result.TryGetValue(kvp.Key, out var current) ||
                current.Source < TranslationSource.Manual)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        // Preserve existing third-party translations that shouldn't be updated
        foreach (var kvp in existing.Where(e => e.Value.Source == TranslationSource.ThirdParty))
        {
            if (result.TryGetValue(kvp.Key, out var current) &&
                current.Source == TranslationSource.ThirdParty)
            {
                // Keep the original third-party translation
                result[kvp.Key] = kvp.Value;
            }
        }

        // Apply jw.org translations (highest priority - always overwrites)
        if (jwOrgTranslations != null)
        {
            foreach (var kvp in jwOrgTranslations)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    result[kvp.Key] = new TrackedTranslation
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        Source = TranslationSource.JwOrg
                    };
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Update tracking data with merged translations
    /// </summary>
    public void UpdateTrackingData(string cultureCode, Dictionary<string, TrackedTranslation> translations)
    {
        var data = LoadTrackingData(cultureCode);
        data.Translations = translations;
        data.LastUpdated = DateTime.UtcNow;
        SaveTrackingData(cultureCode);
    }

    private Dictionary<string, string> LoadBaseValues(string baseResxPath)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var doc = XDocument.Load(baseResxPath);
            var root = doc.Root;
            if (root == null) return result;

            foreach (var data in root.Elements("data"))
            {
                var key = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;

                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    result[key] = value;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load base resx values");
        }

        return result;
    }
}
