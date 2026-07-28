using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Platform.Windows.Tests.Fakes;

public sealed class FakeDisplayService : IDisplayService
{
    public required DisplaySnapshot Snapshot { get; init; }

    public DisplaySnapshot GetSnapshot() => Snapshot;
}
