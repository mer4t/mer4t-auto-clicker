using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Domain.Automation;

public sealed record ClickPlan
{
    public required MouseButton Button { get; init; }

    public required ClickType ClickType { get; init; }

    public required TimeSpan Interval { get; init; }

    public required RepeatMode RepeatMode { get; init; }

    public int? RepeatCount { get; init; }

    public required CoordinateTarget Target { get; init; }

    public TimeSpan StartDelay { get; init; } = TimeSpan.Zero;
}
