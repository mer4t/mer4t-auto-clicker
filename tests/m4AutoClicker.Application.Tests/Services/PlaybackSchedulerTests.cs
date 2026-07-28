using m4AutoClicker.Application.Services;
using m4AutoClicker.Application.Tests.Fakes;

namespace m4AutoClicker.Application.Tests.Services;

public class PlaybackSchedulerTests
{
    [Fact]
    public async Task WaitUntilAsync_Returns_Immediately_When_Target_Already_Passed()
    {
        var clock = new FakeHighResolutionClock { CurrentTimestamp = 1000, Frequency = 10_000_000 };
        var scheduler = new PlaybackScheduler(clock);

        var task = scheduler.WaitUntilAsync(500, CancellationToken.None);

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.Same(task, completed);
    }

    [Fact]
    public async Task WaitUntilAsync_Throws_When_Already_Cancelled()
    {
        var clock = new FakeHighResolutionClock { CurrentTimestamp = 0, Frequency = 10_000_000 };
        var scheduler = new PlaybackScheduler(clock);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scheduler.WaitUntilAsync(1_000_000_000, cts.Token));
    }

    [Fact]
    public async Task WaitUntilAsync_Waits_Approximately_The_Requested_Duration()
    {
        var clock = new RealElapsedClock();
        var scheduler = new PlaybackScheduler(clock);

        var target = clock.GetTimestamp() + (long)(0.03 * clock.Frequency);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await scheduler.WaitUntilAsync(target, CancellationToken.None);
        sw.Stop();

        Assert.InRange(sw.Elapsed.TotalMilliseconds, 15, 500);
    }
}
