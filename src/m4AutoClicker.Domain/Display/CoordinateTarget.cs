namespace m4AutoClicker.Domain.Display;

public abstract record CoordinateTarget
{
    public static CoordinateTarget CurrentCursor { get; } = new CurrentCursorTarget();

    public static CoordinateTarget FixedPoint(ScreenPoint point) => new FixedPointTarget { Point = point };
}

public sealed record CurrentCursorTarget : CoordinateTarget;

public sealed record FixedPointTarget : CoordinateTarget
{
    public required ScreenPoint Point { get; init; }
}
