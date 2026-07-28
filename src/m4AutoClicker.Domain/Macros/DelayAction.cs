namespace m4AutoClicker.Domain.Macros;

public sealed record DelayAction : MacroAction
{
    public required long DurationTicks { get; init; }
}
