using MertClicker.Application.Abstractions;
using MertClicker.Domain.Display;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeDisplayService : IDisplayService
{
    public DisplaySnapshot Snapshot { get; set; } = new()
    {
        VirtualLeft = 0,
        VirtualTop = 0,
        VirtualWidth = 1920,
        VirtualHeight = 1080,
        Monitors =
        [
            new MonitorSnapshot
            {
                DeviceId = "PRIMARY",
                Bounds = new MonitorBounds(0, 0, 1920, 1080),
                WorkingArea = new MonitorBounds(0, 0, 1920, 1040),
                DpiX = 96,
                DpiY = 96,
                IsPrimary = true
            }
        ]
    };

    public DisplaySnapshot GetSnapshot() => Snapshot;
}
