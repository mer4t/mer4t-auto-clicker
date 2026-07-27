using MertClicker.Application.Abstractions;
using MertClicker.Domain.Display;

namespace MertClicker.Platform.Windows.Tests.Fakes;

public sealed class FakeDisplayService : IDisplayService
{
    public required DisplaySnapshot Snapshot { get; init; }

    public DisplaySnapshot GetSnapshot() => Snapshot;
}
