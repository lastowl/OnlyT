namespace JwTranslationExtractor.Models;

/// <summary>
/// Represents a translation extracted from jw.org
/// </summary>
public class ExtractedTranslation
{
    /// <summary>
    /// The resource key (e.g., "SECTION_TREASURES", "TALK_READING")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The translated value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The language code this translation is for
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Source URL where the translation was extracted from
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Whether this is an abbreviation of a longer string
    /// </summary>
    public bool IsAbbreviated { get; set; }
}

/// <summary>
/// Collection of translations for a specific language
/// </summary>
public class LanguageTranslations
{
    public string LanguageCode { get; set; } = string.Empty;
    public string CultureCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public List<ExtractedTranslation> Translations { get; set; } = new();
}
