using System;

namespace OnlyT.Avalonia.EventArgsTypes;

/// <summary>
/// Event args when a timer is started
/// </summary>
public class TimerStartedEventArgs : EventArgs
{
    public int TargetSecs { get; set; }
    public int ClosingSecs { get; set; }
}
