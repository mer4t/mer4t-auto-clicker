using m4AutoClicker.Application.Abstractions;

namespace m4AutoClicker.Application.Tests.Fakes;

public sealed class FakeHighResolutionClock : IHighResolutionClock
{
    public long CurrentTimestamp { get; set; }

    public long Frequency { get; set; } = 10_000_000;

    public long GetTimestamp() => CurrentTimestamp;
}
