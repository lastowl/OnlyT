using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    // The base Resources.resx is British English
    private const string DefaultCulture = "en-GB";

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
        return GetTranslatedCultures()
            .Select(CreateLanguageItem)
            .OrderBy(item => item.NativeName, StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true))
            .ToList();
    }

    /// <summary>
    /// Every culture with a satellite resource assembly next to the app, plus the default culture.
    /// </summary>
    private static IEnumerable<string> GetTranslatedCultures()
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultCulture };

        var assembly = typeof(LocalizationService).Assembly;
        var satelliteName = $"{assembly.GetName().Name}.resources.dll";
        var baseDirectory = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(baseDirectory))
            {
                var name = Path.GetFileName(directory);

                // The neutral English resources duplicate the default culture
                if (name.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(Path.Combine(directory, satelliteName)))
                {
                    continue;
                }

                cultures.Add(name);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate translations in {Directory}", baseDirectory);
        }

        return cultures;
    }

    private static LanguageItem CreateLanguageItem(string cultureCode)
    {
        try
        {
            var culture = new CultureInfo(cultureCode);
            return new LanguageItem
            {
                CultureCode = cultureCode,
                DisplayName = culture.DisplayName,
                NativeName = culture.NativeName
            };
        }
        catch (CultureNotFoundException)
        {
            // Culture not supported on this system
            return new LanguageItem
            {
                CultureCode = cultureCode,
                DisplayName = cultureCode,
                NativeName = cultureCode
            };
        }
    }
}
