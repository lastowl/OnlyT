using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlyT.Avalonia.Services;
using OnlyT.Avalonia.Services.Options;

namespace OnlyT.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the countdown window that displays time until meeting starts
/// </summary>
public partial class CountdownViewModel : ObservableObject
{
    private Action? _closeAction;
    private Action? _timeUpAction;

    [ObservableProperty]
    private int _countdownTotalSeconds = 300;

    [ObservableProperty]
    private ElementsToShow _elementsToShow = ElementsToShow.DialAndDigital;

    public CountdownViewModel(IOptionsService optionsService)
    {
        ElementsToShow = optionsService.CountdownElementsToShow;
    }

    // Parameterless constructor for design-time
    public CountdownViewModel()
    {
    }

    public void SetCallbacks(Action? closeAction, Action? timeUpAction)
    {
        _closeAction = closeAction;
        _timeUpAction = timeUpAction;
    }

    public void OnTimeUp()
    {
        _timeUpAction?.Invoke();
    }

    [RelayCommand]
    private void Close()
    {
        _closeAction?.Invoke();
    }
}
