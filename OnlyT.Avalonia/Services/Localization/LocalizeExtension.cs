using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace OnlyT.Avalonia.Services.Localization;

/// <summary>
/// XAML markup extension for localized strings with live updates
/// Usage: Text="{loc:Localize SETTINGS}"
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// The resource key to look up
    /// </summary>
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
        {
            return "[Missing Key]";
        }

        try
        {
            // Create a LocalizedString that will update when culture changes
            var localizedString = new LocalizedString(Key);

            // Return a binding to the Value property so it updates automatically
            // In Avalonia, returning the binding directly allows the framework to process it
            var binding = new Binding
            {
                Source = localizedString,
                Path = nameof(LocalizedString.Value),
                Mode = BindingMode.OneWay
            };

            return binding;
        }
        catch
        {
            // Design time or service not available - return static value
            try
            {
                var localizationService = Ioc.Default.GetService<ILocalizationService>();
                if (localizationService != null)
                {
                    return localizationService.GetString(Key);
                }
            }
            catch
            {
                // Fallback
            }
        }

        return $"[{Key}]";
    }
}
