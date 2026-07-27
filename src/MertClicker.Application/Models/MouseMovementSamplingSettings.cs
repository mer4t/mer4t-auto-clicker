namespace MertClicker.Application.Models;

public sealed record MouseMovementSamplingSettings
{
    public int MinimumIntervalMilliseconds { get; init; } = 8;

    public int MinimumDistancePixels { get; init; } = 2;
}
