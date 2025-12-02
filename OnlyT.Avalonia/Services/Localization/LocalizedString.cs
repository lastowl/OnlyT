using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace OnlyT.Avalonia.Services.Localization;

/// <summary>
/// Observable localized string that updates when the culture changes.
/// Implements IDisposable to allow proper cleanup of event subscriptions.
/// </summary>
/// <remarks>
/// This class subscribes to the <see cref="ILocalizationService.CultureChanged"/> event
/// to automatically update its value when the application language changes.
/// Call <see cref="Dispose"/> when the localized string is no longer needed to prevent memory leaks.
/// </remarks>
public class LocalizedString : INotifyPropertyChanged, IDisposable
{
    private readonly string _key;
    private readonly ILocalizationService? _localizationService;
    private string _value;
    private bool _disposed;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Creates a new localized string for the specified resource key.
    /// </summary>
    /// <param name="key">The resource key to look up in the localization service.</param>
    public LocalizedString(string key)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _localizationService = Ioc.Default.GetService<ILocalizationService>();
        _value = GetLocalizedValue();

        if (_localizationService != null)
        {
            _localizationService.CultureChanged += OnCultureChanged;
        }
    }

    /// <summary>
    /// Gets the current localized value for this string.
    /// </summary>
    /// <value>The localized string value, or the key in brackets if not found.</value>
    public string Value
    {
        get => _value;
        private set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    /// <summary>
    /// Gets the resource key for this localized string.
    /// </summary>
    public string Key => _key;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            Value = GetLocalizedValue();
        }
    }

    private string GetLocalizedValue()
    {
        return _localizationService?.GetString(_key) ?? $"[{_key}]";
    }

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a <see cref="LocalizedString"/> to its string value.
    /// </summary>
    /// <param name="ls">The localized string to convert.</param>
    public static implicit operator string(LocalizedString ls) => ls.Value;

    /// <summary>
    /// Releases all resources used by this <see cref="LocalizedString"/> and
    /// unsubscribes from the culture changed event.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Unsubscribe from the event to prevent memory leaks
            if (_localizationService != null)
            {
                _localizationService.CultureChanged -= OnCultureChanged;
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer to ensure cleanup if Dispose is not called.
    /// </summary>
    ~LocalizedString()
    {
        Dispose(false);
    }
}
