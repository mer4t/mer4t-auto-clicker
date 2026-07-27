using System.Diagnostics;
using MertClicker.Application.Abstractions;

namespace MertClicker.Application.Tests.Fakes;

// AutomationEngine testlerinde gerçek zamanlı bekleme davranışını (Task.Delay tabanlı) doğrulamak için
// gerçekten ilerleyen bir saat gerekir; sabit bir FakeHighResolutionClock ile zamanlayıcı sonsuz döngüye girer.
public sealed class RealElapsedClock : IHighResolutionClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public long GetTimestamp() => _stopwatch.ElapsedTicks;

    public long Frequency => Stopwatch.Frequency;
}
