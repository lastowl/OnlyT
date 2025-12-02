using System.Collections.Generic;
using System.Globalization;

namespace OnlyT.Avalonia.Services.Localization;

/// <summary>
/// Service for managing application localization
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Get a localized string by key
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// Get a localized string by key
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Get a localized string with format parameters
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Current culture
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Set the current culture
    /// </summary>
    void SetCulture(string cultureCode);

    /// <summary>
    /// Get all supported languages
    /// </summary>
    IEnumerable<LanguageItem> GetSupportedLanguages();

    /// <summary>
    /// Event raised when the culture changes
    /// </summary>
    event System.EventHandler? CultureChanged;
}

/// <summary>
/// Represents a supported language
/// </summary>
public class LanguageItem
{
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}
