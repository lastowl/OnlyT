using OnlyT.Avalonia.Models;

namespace OnlyT.Avalonia.EventArgsTypes;

public class TimerInfoEventArgs : System.EventArgs
{
    public ClockServerMode Mode { get; set; }

    public int Mins { get; set; }

    public int Secs { get; set; }

    public int Millisecs { get; set; }

    public int TargetSecs { get; set; }

    public bool Use24HrFormat { get; set; }

    public bool IsCountingUp { get; set; }

    public int ClosingSecs { get; set; }
}