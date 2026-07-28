using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Domain.Macros;

public sealed record MouseMoveAction : MacroAction
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public CoordinateMode CoordinateMode { get; init; } = CoordinateMode.AbsoluteDesktop;
}
