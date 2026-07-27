using MertClicker.Application.Abstractions;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeEmergencyStopService : IEmergencyStopService
{
    public int CallCount { get; private set; }

    public bool ThrowOnStop { get; set; }

    public Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (ThrowOnStop)
        {
            throw new InvalidOperationException("Simüle edilmiş acil durdurma hatası.");
        }

        return Task.CompletedTask;
    }
}
