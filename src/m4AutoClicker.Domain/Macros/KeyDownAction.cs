namespace m4AutoClicker.Domain.Macros;

public sealed record KeyDownAction : MacroAction
{
    public required ushort KeyCode { get; init; }
}
