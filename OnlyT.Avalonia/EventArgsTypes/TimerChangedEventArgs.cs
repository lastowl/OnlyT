namespace OnlyT.Avalonia.EventArgsTypes;

/// <inheritdoc />
/// <summary>
/// Event args for change in timer values
/// </summary>
public class TimerChangedEventArgs : System.EventArgs
{
    public int TargetSecs { get; init; }

    public int ElapsedSecs { get; init; }

    public bool IsRunning { get; init; }

    public int ClosingSecs { get; init; }

    /// <summary>
    /// Whether the current run is counting up rather than down. The output
    /// window needs this to decide whether to display elapsed or remaining
    /// seconds — without it the output window always shows the countdown
    /// even when the operator page is in count-up mode.
    /// </summary>
    public bool IsCountingUp { get; init; }

    public int RemainingSecs => TargetSecs - ElapsedSecs;
}