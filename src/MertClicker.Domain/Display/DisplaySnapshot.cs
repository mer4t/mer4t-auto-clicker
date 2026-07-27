namespace MertClicker.Domain.Display;

public sealed record DisplaySnapshot
{
    public required int VirtualLeft { get; init; }

    public required int VirtualTop { get; init; }

    public required int VirtualWidth { get; init; }

    public required int VirtualHeight { get; init; }

    public required IReadOnlyList<MonitorSnapshot> Monitors { get; init; }
}
