namespace m4AutoClicker.Domain.Macros;

public sealed record MouseButtonUpAction : MacroAction
{
    public required MouseButton Button { get; init; }
}
