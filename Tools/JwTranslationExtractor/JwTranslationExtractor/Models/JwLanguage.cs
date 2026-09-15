namespace JwTranslationExtractor.Models;

/// <summary>
/// Represents a language from jw.org
/// </summary>
public class JwLanguage
{
    public string Symbol { get; set; } = string.Empty;
    public string LangCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VernacularName { get; set; } = string.Empty;
    public bool IsSignLanguage { get; set; }
    public bool HasWebContent { get; set; }

    /// <summary>
    /// .NET culture code (e.g., "en-GB", "de-DE")
    /// </summary>
    public string? CultureCode { get; set; }

    /// <summary>
    /// jw.org locale (e.g., "de", "pt-pt", "cmn-hans")
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// jw.org script name (e.g., "ROMAN", "CYRILLIC")
    /// </summary>
    public string? Script { get; set; }

    /// <summary>
    /// Whether this language is supported for translation extraction
    /// </summary>
    public bool IsSupported => !IsSignLanguage && HasWebContent;
}
