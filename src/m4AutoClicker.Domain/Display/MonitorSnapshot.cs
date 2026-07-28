namespace m4AutoClicker.Domain.Display;

public sealed record MonitorSnapshot
{
    public required string DeviceId { get; init; }

    public required MonitorBounds Bounds { get; init; }

    public required MonitorBounds WorkingArea { get; init; }

    public required int DpiX { get; init; }

    public required int DpiY { get; init; }

    public required bool IsPrimary { get; init; }
}
