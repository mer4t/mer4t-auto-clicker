using MertClicker.Domain.Display;

namespace MertClicker.Domain.Macros;

public sealed record MouseMoveAction : MacroAction
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public CoordinateMode CoordinateMode { get; init; } = CoordinateMode.AbsoluteDesktop;
}
