namespace JwTranslationExtractor.Models;

/// <summary>
/// Indicates the source of a translation value.
/// Priority order (highest to lowest): JwOrg > Manual > ThirdParty > Fallback
/// </summary>
public enum TranslationSource
{
    /// <summary>
    /// English fallback value (not translated)
    /// </summary>
    Fallback = 0,

    /// <summary>
    /// Translated by a third-party service (e.g., Google Translate, LibreTranslate)
    /// Should not be updated once initially populated.
    /// </summary>
    ThirdParty = 1,

    /// <summary>
    /// Manually added translation in the resx file.
    /// Preserved unless jw.org translation is available.
    /// </summary>
    Manual = 2,

    /// <summary>
    /// Official translation from jw.org.
    /// Highest priority - always overwrites other sources.
    /// </summary>
    JwOrg = 3
}

/// <summary>
/// Represents a translation entry with source tracking
/// </summary>
public class TrackedTranslation
{
    /// <summary>
    /// The resource key (e.g., "SETTINGS", "BELL_ENABLED")
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The translated value
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// The source of this translation
    /// </summary>
    public TranslationSource Source { get; set; }

    /// <summary>
    /// When this translation was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Translation tracking data for a language
/// </summary>
public class LanguageTranslationData
{
    /// <summary>
    /// The culture/language code (e.g., "fr-FR", "es-ES")
    /// </summary>
    public required string CultureCode { get; set; }

    /// <summary>
    /// All tracked translations for this language
    /// </summary>
    public Dictionary<string, TrackedTranslation> Translations { get; set; } = new();

    /// <summary>
    /// When this language data was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
