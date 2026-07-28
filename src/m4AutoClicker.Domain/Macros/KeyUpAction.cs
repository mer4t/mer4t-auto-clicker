namespace m4AutoClicker.Domain.Macros;

public sealed record KeyUpAction : MacroAction
{
    public required ushort KeyCode { get; init; }
}
