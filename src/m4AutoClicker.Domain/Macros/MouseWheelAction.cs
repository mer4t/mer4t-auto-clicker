namespace m4AutoClicker.Domain.Macros;

public sealed record MouseWheelAction : MacroAction
{
    public required int Delta { get; init; }
}
