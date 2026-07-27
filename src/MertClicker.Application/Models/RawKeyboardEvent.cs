namespace MertClicker.Application.Models;

public sealed record RawKeyboardEvent
{
    public required RawKeyboardEventType EventType { get; init; }

    public required ushort KeyCode { get; init; }

    public required long TimestampTicks { get; init; }

    public required bool IsInjectedByApplication { get; init; }
}
