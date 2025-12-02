using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Threading;
using Serilog;

namespace OnlyT.Avalonia.Services.Localization;

/// <summary>
/// Localization service implementation using ResourceManager
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    // List of supported culture codes (matching the resx files)
    private static readonly string[] SupportedCultures =
    [
        "en-GB",  // Default (base Resources.resx)
        "en-US",
        "es-ES",
        "es-MX",
        "pt-PT",
        "pt-BR",
        "fr-FR",
        "de-DE",
        "it-IT",
        "nl-NL",
        "sv-SE",
        "no-NO",
        "pl-PL",
        "cs-CZ",
        "sk-SK",
        "ru-RU",
        "uk-UA",
        "el-GR",
        "hu-HU",
        "ro-RO",
        "hr-HR",
        "fi-FI",
        "lv-LV",
        "tr-TR",
        "ko-KR",
        "vi-VN",
        "id-ID",
        "fil-PH",
        "ka-GE",
        "ca-ES",
        "jv-ID",
        "pap"
    ];

    public event EventHandler? CultureChanged;

    public LocalizationService()
    {
        _resourceManager = new ResourceManager(
            "OnlyT.Avalonia.Properties.Resources",
            typeof(LocalizationService).Assembly);
        _currentCulture = CultureInfo.CurrentUICulture;
    }

    public string this[string key] => GetString(key);

    public CultureInfo CurrentCulture => _currentCulture;

    public string GetString(string key)
    {
        try
        {
            var value = _resourceManager.GetString(key, _currentCulture);
            return value ?? key;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get localized string for key: {Key}", key);
            return key;
        }
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    public void SetCulture(string cultureCode)
    {
        try
        {
            var culture = new CultureInfo(cultureCode);
            _currentCulture = culture;

            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            Log.Information("Culture set to {Culture}", cultureCode);
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set culture to {Culture}", cultureCode);
        }
    }

    public IEnumerable<LanguageItem> GetSupportedLanguages()
    {
        foreach (var cultureCode in SupportedCultures)
        {
            LanguageItem? item = null;
            try
            {
                var culture = new CultureInfo(cultureCode);
                item = new LanguageItem
                {
                    CultureCode = cultureCode,
                    DisplayName = culture.DisplayName,
                    NativeName = culture.NativeName
                };
            }
            catch
            {
                // Culture not supported on this system
                item = new LanguageItem
                {
                    CultureCode = cultureCode,
                    DisplayName = cultureCode,
                    NativeName = cultureCode
                };
            }

            if (item != null)
            {
                yield return item;
            }
        }
    }
}
