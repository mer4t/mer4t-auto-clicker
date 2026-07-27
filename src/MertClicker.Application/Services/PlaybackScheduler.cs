using MertClicker.Application.Abstractions;

namespace MertClicker.Application.Services;

public sealed class PlaybackScheduler
{
    private const double LongWaitThresholdMilliseconds = 15.0;
    private const double ShortWaitThresholdMilliseconds = 2.0;

    private readonly IHighResolutionClock _clock;

    public PlaybackScheduler(IHighResolutionClock clock)
    {
        _clock = clock;
    }

    public async Task WaitUntilAsync(long targetTimestamp, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remainingTicks = targetTimestamp - _clock.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remainingMilliseconds = remainingTicks * 1000.0 / _clock.Frequency;

            if (remainingMilliseconds > LongWaitThresholdMilliseconds)
            {
                // Uzun beklemede kaba Task.Delay yeterli; son 15 ms'lik pay daha hassas bekleme için ayrılır.
                await Task.Delay(
                    TimeSpan.FromMilliseconds(remainingMilliseconds - LongWaitThresholdMilliseconds),
                    cancellationToken);
            }
            else if (remainingMilliseconds > ShortWaitThresholdMilliseconds)
            {
                await Task.Delay(1, cancellationToken);
            }
            else
            {
                // Hedefe çok yakınken kısa, kontrollü bir spin ile CPU'yu gereksiz meşgul etmeden bekler.
                Thread.SpinWait(100);
            }
        }
    }
}
