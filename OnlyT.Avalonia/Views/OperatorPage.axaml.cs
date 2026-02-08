using Avalonia.Controls;
using Avalonia.Input;
using OnlyT.Avalonia.ViewModels;

namespace OnlyT.Avalonia.Views;

public partial class OperatorPage : UserControl
{
    public OperatorPage()
    {
        InitializeComponent();

        var timerBorder = this.FindControl<Border>("TimerDisplayBorder");
        if (timerBorder != null)
        {
            timerBorder.PointerWheelChanged += OnTimerWheelChanged;
        }
    }

    private void OnTimerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not OperatorPageViewModel vm)
            return;

        var delta = e.Delta.Y;
        if (delta == 0)
            return;

        var modifiers = e.KeyModifiers;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift + wheel: 5 minute increments
            vm.AdjustTimerByWheel(delta > 0 ? 300 : -300);
        }
        else if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
        {
            // Ctrl/Cmd + wheel: 15 second increments
            vm.AdjustTimerByWheel(delta > 0 ? 15 : -15);
        }
        else
        {
            // Normal wheel: 1 minute increments
            vm.AdjustTimerByWheel(delta > 0 ? 60 : -60);
        }

        e.Handled = true;
    }
}
