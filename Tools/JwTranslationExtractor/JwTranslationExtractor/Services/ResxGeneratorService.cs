using System.Xml.Linq;
using JwTranslationExtractor.Models;
using Serilog;

namespace JwTranslationExtractor.Services;

/// <summary>
/// Service for generating .resx resource files with translation tracking
/// </summary>
public class ResxGeneratorService
{
    private readonly TranslationTrackingService? _trackingService;
    private readonly GoogleTranslateService? _googleTranslateService;
    private readonly FreeTranslationService? _freeTranslationService;

    public ResxGeneratorService(
        TranslationTrackingService? trackingService = null,
        GoogleTranslateService? googleTranslateService = null,
        FreeTranslationService? freeTranslationService = null)
    {
        _trackingService = trackingService;
        _googleTranslateService = googleTranslateService;
        _freeTranslationService = freeTranslationService;
    }

    /// <summary>
    /// Generates a .resx file for a specific language with extracted translations
    /// </summary>
    public void GenerateResxFile(
        string baseResxPath,
        string outputDirectory,
        LanguageTranslations translations)
    {
        try
        {
            // Load the base resx file
            var baseDoc = XDocument.Load(baseResxPath);

            // Create a new document based on the base
            var newDoc = new XDocument(baseDoc);

            // Apply translations
            var root = newDoc.Root;
            if (root == null) return;

            foreach (var translation in translations.Translations)
            {
                // Find existing data element with this key
                var existingData = root.Elements("data")
                    .FirstOrDefault(e => e.Attribute("name")?.Value == translation.Key);

                if (existingData != null)
                {
                    // Update the value
                    var valueElement = existingData.Element("value");
                    if (valueElement != null)
                    {
                        valueElement.Value = translation.Value;
                    }
                }
            }

            // Generate output file path
            var cultureCode = translations.CultureCode;
            var outputPath = Path.Combine(outputDirectory, $"Resources.{cultureCode}.resx");

            // Ensure directory exists
            Directory.CreateDirectory(outputDirectory);

            // Save the file
            newDoc.Save(outputPath);

            Log.Information("Generated {Path} with {Count} translations",
                outputPath, translations.Translations.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate resx file for {Culture}", translations.CultureCode);
            throw;
        }
    }

    /// <summary>
    /// Creates a new resx file with translation hierarchy:
    /// 1. jw.org translations (highest priority)
    /// 2. Manual translations from existing file
    /// 3. Third-party translations (only if not already present)
    /// 4. English fallback (lowest priority)
    /// </summary>
    public async Task GenerateResxFileWithHierarchyAsync(
        string baseResxPath,
        string outputDirectory,
        string cultureCode,
        Dictionary<string, string>? jwOrgTranslations = null,
        bool useThirdPartyTranslation = false)
    {
        try
        {
            // Load base English values
            var baseValues = LoadBaseValues(baseResxPath);

            // Check for existing resx file
            var existingResxPath = Path.Combine(outputDirectory, $"Resources.{cultureCode}.resx");
            var existingTranslations = new Dictionary<string, TrackedTranslation>();

            if (_trackingService != null && File.Exists(existingResxPath))
            {
                existingTranslations = _trackingService.ReadExistingTranslations(
                    existingResxPath, baseResxPath, cultureCode);
            }

            // Get third-party translations if needed
            Dictionary<string, string>? thirdPartyTranslations = null;

            if (useThirdPartyTranslation && (_googleTranslateService != null || _freeTranslationService != null))
            {
                // Only translate keys that don't have jw.org, manual, or existing third-party translations
                var keysNeedingTranslation = baseValues
                    .Where(kvp =>
                        (jwOrgTranslations == null || !jwOrgTranslations.ContainsKey(kvp.Key)) &&
                        (!existingTranslations.TryGetValue(kvp.Key, out var existing) ||
                         existing.Source == TranslationSource.Fallback))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (keysNeedingTranslation.Count > 0)
                {
                    // Use Google Translate if available (preferred)
                    if (_googleTranslateService != null && GoogleTranslateService.IsLanguageSupported(cultureCode))
                    {
                        Log.Information("Translating {Count} keys for {Culture} using Google Translate...",
                            keysNeedingTranslation.Count, cultureCode);

                        thirdPartyTranslations = await _googleTranslateService.TranslateBatchAsync(
                            keysNeedingTranslation, cultureCode);

                        Log.Information("Got {Count} Google translations for {Culture}",
                            thirdPartyTranslations.Count, cultureCode);
                    }
                    // Fallback to free translation service
                    else if (_freeTranslationService != null && FreeTranslationService.IsLanguageSupported(cultureCode))
                    {
                        Log.Information("Translating {Count} keys for {Culture} using free translation service...",
                            keysNeedingTranslation.Count, cultureCode);

                        thirdPartyTranslations = await _freeTranslationService.TranslateBatchAsync(
                            keysNeedingTranslation, cultureCode);

                        Log.Information("Got {Count} free translations for {Culture}",
                            thirdPartyTranslations.Count, cultureCode);
                    }
                }
            }

            // Merge translations according to hierarchy
            Dictionary<string, TrackedTranslation> mergedTranslations;

            if (_trackingService != null)
            {
                mergedTranslations = _trackingService.MergeTranslations(
                    existingTranslations,
                    jwOrgTranslations,
                    thirdPartyTranslations,
                    baseValues);

                // Update tracking data
                _trackingService.UpdateTrackingData(cultureCode, mergedTranslations);
            }
            else
            {
                // Simple merge without tracking
                mergedTranslations = baseValues.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new TrackedTranslation
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        Source = TranslationSource.Fallback
                    });

                // Apply jw.org translations
                if (jwOrgTranslations != null)
                {
                    foreach (var kvp in jwOrgTranslations)
                    {
                        if (!string.IsNullOrEmpty(kvp.Value))
                        {
                            mergedTranslations[kvp.Key] = new TrackedTranslation
                            {
                                Key = kvp.Key,
                                Value = kvp.Value,
                                Source = TranslationSource.JwOrg
                            };
                        }
                    }
                }
            }

            // Generate the resx file
            GenerateResxFromTrackedTranslations(baseResxPath, outputDirectory, cultureCode, mergedTranslations);

            // Log statistics
            var stats = mergedTranslations.Values
                .GroupBy(t => t.Source)
                .ToDictionary(g => g.Key, g => g.Count());

            Log.Information("Generated {Path}: JwOrg={JwOrg}, Manual={Manual}, ThirdParty={ThirdParty}, Fallback={Fallback}",
                $"Resources.{cultureCode}.resx",
                stats.GetValueOrDefault(TranslationSource.JwOrg),
                stats.GetValueOrDefault(TranslationSource.Manual),
                stats.GetValueOrDefault(TranslationSource.ThirdParty),
                stats.GetValueOrDefault(TranslationSource.Fallback));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate resx file for {Culture}", cultureCode);
            throw;
        }
    }

    /// <summary>
    /// Creates a new resx file with English fallback values for all keys,
    /// then applies any available translations
    /// </summary>
    public void GenerateResxFileWithFallback(
        string baseResxPath,
        string outputDirectory,
        string cultureCode,
        LanguageTranslations? translations = null)
    {
        try
        {
            // Load the base English resx file
            var baseDoc = XDocument.Load(baseResxPath);

            // Create a copy (this will have English values as fallback)
            var newDoc = new XDocument(baseDoc);
            var root = newDoc.Root;
            if (root == null) return;

            // Apply translations if we have them
            if (translations != null)
            {
                foreach (var translation in translations.Translations)
                {
                    var existingData = root.Elements("data")
                        .FirstOrDefault(e => e.Attribute("name")?.Value == translation.Key);

                    if (existingData != null)
                    {
                        var valueElement = existingData.Element("value");
                        if (valueElement != null && !string.IsNullOrEmpty(translation.Value))
                        {
                            valueElement.Value = translation.Value;
                        }
                    }
                }
            }

            // Generate output file path
            var outputPath = Path.Combine(outputDirectory, $"Resources.{cultureCode}.resx");

            // Ensure directory exists
            Directory.CreateDirectory(outputDirectory);

            // Save the file
            newDoc.Save(outputPath);

            var translationCount = translations?.Translations.Count ?? 0;
            Log.Information("Generated {Path} ({Count} translations, rest fallback to English)",
                outputPath, translationCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate resx file for {Culture}", cultureCode);
            throw;
        }
    }

    /// <summary>
    /// Generates resx files for all supported languages with hierarchy
    /// </summary>
    public async Task GenerateAllLanguageFilesWithHierarchyAsync(
        string baseResxPath,
        string outputDirectory,
        List<JwLanguage> languages,
        Dictionary<string, LanguageTranslations> jwTranslations,
        bool useThirdPartyTranslation = false)
    {
        var totalCount = languages.Count;
        var currentCount = 0;

        foreach (var language in languages)
        {
            currentCount++;
            var cultureCode = language.CultureCode ?? language.LangCode;

            Log.Information("[{Current}/{Total}] Processing {Culture} ({Name})",
                currentCount, totalCount, cultureCode, language.VernacularName);

            // Get jw.org translations for this language
            Dictionary<string, string>? jwOrgDict = null;
            if (jwTranslations.TryGetValue(language.LangCode, out var langTranslations))
            {
                jwOrgDict = langTranslations.Translations
                    .ToDictionary(t => t.Key, t => t.Value);
            }

            await GenerateResxFileWithHierarchyAsync(
                baseResxPath,
                outputDirectory,
                cultureCode,
                jwOrgDict,
                useThirdPartyTranslation);

            // Small delay between languages
            await Task.Delay(50);
        }

        Log.Information("Processed {Count} language files in {Directory}",
            languages.Count, outputDirectory);
    }

    /// <summary>
    /// Generates resx files for all supported languages (legacy method)
    /// </summary>
    public async Task GenerateAllLanguageFilesAsync(
        string baseResxPath,
        string outputDirectory,
        List<JwLanguage> languages,
        Dictionary<string, LanguageTranslations> translations)
    {
        var totalCount = languages.Count;
        var currentCount = 0;

        foreach (var language in languages)
        {
            currentCount++;
            var cultureCode = language.CultureCode ?? language.LangCode;

            Log.Information("[{Current}/{Total}] Generating resx for {Culture} ({Name})",
                currentCount, totalCount, cultureCode, language.VernacularName);

            translations.TryGetValue(language.LangCode, out var langTranslations);

            GenerateResxFileWithFallback(baseResxPath, outputDirectory, cultureCode, langTranslations);

            // Small delay to not overwhelm the system
            await Task.Delay(10);
        }

        Log.Information("Generated {Count} language files in {Directory}",
            languages.Count, outputDirectory);
    }

    private void GenerateResxFromTrackedTranslations(
        string baseResxPath,
        string outputDirectory,
        string cultureCode,
        Dictionary<string, TrackedTranslation> translations)
    {
        var baseDoc = XDocument.Load(baseResxPath);
        var newDoc = new XDocument(baseDoc);
        var root = newDoc.Root;

        if (root == null) return;

        foreach (var kvp in translations)
        {
            var existingData = root.Elements("data")
                .FirstOrDefault(e => e.Attribute("name")?.Value == kvp.Key);

            if (existingData != null)
            {
                var valueElement = existingData.Element("value");
                if (valueElement != null)
                {
                    valueElement.Value = kvp.Value.Value;
                }
            }
        }

        var outputPath = Path.Combine(outputDirectory, $"Resources.{cultureCode}.resx");
        Directory.CreateDirectory(outputDirectory);
        newDoc.Save(outputPath);
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
