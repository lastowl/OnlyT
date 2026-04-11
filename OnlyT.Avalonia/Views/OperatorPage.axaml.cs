using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OnlyT.Avalonia.ViewModels;

namespace OnlyT.Avalonia.Views;

public partial class OperatorPage : UserControl
{
    // Captured from the most recent PointerPressed on either step button and
    // consumed by the Click handler. Button.Click in Avalonia doesn't carry
    // modifier state, so we snapshot it at press time instead.
    private KeyModifiers _pendingStepModifiers = KeyModifiers.None;

    public OperatorPage()
    {
        InitializeComponent();

        var timerBorder = this.FindControl<Border>("TimerDisplayBorder");
        if (timerBorder != null)
        {
            timerBorder.PointerWheelChanged += OnTimerWheelChanged;
        }

        var incrementBtn = this.FindControl<Button>("IncrementTimeButton");
        if (incrementBtn != null)
        {
            incrementBtn.AddHandler(InputElement.PointerPressedEvent, OnStepButtonPressed, RoutingStrategies.Tunnel);
            incrementBtn.Click += (_, e) => OnTimerStepClick(e, positive: true);
        }

        var decrementBtn = this.FindControl<Button>("DecrementTimeButton");
        if (decrementBtn != null)
        {
            decrementBtn.AddHandler(InputElement.PointerPressedEvent, OnStepButtonPressed, RoutingStrategies.Tunnel);
            decrementBtn.Click += (_, e) => OnTimerStepClick(e, positive: false);
        }
    }

    private void OnTimerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not OperatorPageViewModel vm)
            return;

        var delta = e.Delta.Y;
        if (delta == 0)
            return;

        var step = StepSecondsForModifiers(e.KeyModifiers);
        vm.AdjustTimerByWheel(delta > 0 ? step : -step);
        e.Handled = true;
    }

    private void OnStepButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        _pendingStepModifiers = e.KeyModifiers;
    }

    // Plain click = 1 min. Ctrl/Cmd = 15 sec. Shift = 5 min. Matches the
    // WPF operator page input bindings so muscle memory carries over.
    private void OnTimerStepClick(RoutedEventArgs e, bool positive)
    {
        if (DataContext is not OperatorPageViewModel vm)
            return;

        var step = StepSecondsForModifiers(_pendingStepModifiers);
        _pendingStepModifiers = KeyModifiers.None;
        vm.AdjustTimerByWheel(positive ? step : -step);
        e.Handled = true;
    }

    private static int StepSecondsForModifiers(KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift))
            return 300;
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
            return 15;
        return 60;
    }
}
