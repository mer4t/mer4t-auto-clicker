namespace m4AutoClicker.Application.Models;

public sealed record RawMouseEvent
{
    public required RawMouseEventType EventType { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public int WheelDelta { get; init; }

    public required long TimestampTicks { get; init; }

    public required bool IsInjectedByApplication { get; init; }
}
