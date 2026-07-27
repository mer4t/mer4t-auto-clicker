namespace MertClicker.Domain.Automation;

public sealed record PlaybackOptions
{
    public double SpeedMultiplier { get; init; } = 1.0;

    public RepeatMode RepeatMode { get; init; } = RepeatMode.FixedCount;

    public int? RepeatCount { get; init; } = 1;

    public TimeSpan StartDelay { get; init; } = TimeSpan.Zero;
}
