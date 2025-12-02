namespace OnlyT.Avalonia.Services.Options;

/// <summary>
/// Adaptive timer mode options
/// </summary>
public enum AdaptiveMode
{
    /// <summary>
    /// No adaptive timing - talks use their original/modified duration
    /// </summary>
    None,

    /// <summary>
    /// One-way mode - only reduces talk times if meeting is running behind schedule
    /// </summary>
    OneWay,

    /// <summary>
    /// Two-way mode - can both shorten and extend talk times to keep meeting on schedule
    /// </summary>
    TwoWay
}
