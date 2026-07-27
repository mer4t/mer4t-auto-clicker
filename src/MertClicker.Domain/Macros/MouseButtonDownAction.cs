namespace MertClicker.Domain.Macros;

public sealed record MouseButtonDownAction : MacroAction
{
    public required MouseButton Button { get; init; }
}
