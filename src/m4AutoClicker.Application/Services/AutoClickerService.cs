using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Services;

public sealed class AutoClickerService
{
    private static readonly TimeSpan ClickHoldDuration = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan DoubleClickGap = TimeSpan.FromMilliseconds(60);

    private readonly AutomationEngine _engine;
    private readonly ICoordinateResolver _coordinateResolver;
    private readonly IDisplayService _displayService;
    private readonly IHighResolutionClock _clock;
    private readonly SemaphoreSlim _startGate = new(1, 1);

    public AutoClickerService(
        AutomationEngine engine,
        ICoordinateResolver coordinateResolver,
        IDisplayService displayService,
        IHighResolutionClock clock)
    {
        _engine = engine;
        _coordinateResolver = coordinateResolver;
        _displayService = displayService;
        _clock = clock;
    }

    public PlaybackState State => _engine.State;

    public async Task<PlaybackResult> StartAsync(ClickPlan plan, CancellationToken cancellationToken)
    {
        // Aynı anda yalnızca tek bir Auto Clicker koşusu başlatılabilir. UI butonu ve F6 global
        // kısayolu aynı akışı kullandığı için bu kapı, aradaki yarış durumlarına karşı korur.
        if (!await _startGate.WaitAsync(0, cancellationToken))
        {
            return new PlaybackResult { Success = false, ExecutedActionCount = 0, ErrorMessage = "Otomasyon zaten çalışıyor." };
        }

        try
        {
            var validationError = Validate(plan);
            if (validationError is not null)
            {
                return new PlaybackResult { Success = false, ExecutedActionCount = 0, ErrorMessage = validationError };
            }

            var displaySnapshot = _displayService.GetSnapshot();
            var resolution = _coordinateResolver.Resolve(plan.Target, displaySnapshot);
            if (!resolution.Success)
            {
                return new PlaybackResult { Success = false, ExecutedActionCount = 0, ErrorMessage = resolution.ErrorMessage };
            }

            var actions = BuildClickTemplate(plan, resolution.Point);

            var options = new PlaybackOptions
            {
                SpeedMultiplier = 1.0,
                RepeatMode = plan.RepeatMode,
                RepeatCount = plan.RepeatMode == RepeatMode.FixedCount ? plan.RepeatCount ?? 1 : null,
                StartDelay = plan.StartDelay
            };

            return await _engine.RunAsync(actions, options, PlaybackState.Clicking, cancellationToken);
        }
        finally
        {
            _startGate.Release();
        }
    }

    public Task StopAsync() => _engine.StopAsync();

    public Task PauseAsync() => _engine.PauseAsync();

    public Task ResumeAsync() => _engine.ResumeAsync();

    private static string? Validate(ClickPlan plan)
    {
        if (plan.Interval <= TimeSpan.Zero)
        {
            return "Tıklama aralığı sıfırdan büyük olmalıdır.";
        }

        if (plan.StartDelay < TimeSpan.Zero)
        {
            return "Başlangıç gecikmesi negatif olamaz.";
        }

        if (plan.RepeatMode == RepeatMode.FixedCount && (plan.RepeatCount is null || plan.RepeatCount <= 0))
        {
            return "Sabit tekrar modu için tekrar sayısı sıfırdan büyük olmalıdır.";
        }

        return null;
    }

    private IReadOnlyList<MacroAction> BuildClickTemplate(ClickPlan plan, ScreenPoint? resolvedPoint)
    {
        var actions = new List<MacroAction>();
        long offset = 0;

        if (resolvedPoint is { } point)
        {
            actions.Add(new MouseMoveAction { OffsetTicks = offset, X = point.X, Y = point.Y });
        }

        offset = AppendClick(actions, plan.Button, offset);

        if (plan.ClickType == ClickType.Double)
        {
            offset += ToTicks(DoubleClickGap);
            offset = AppendClick(actions, plan.Button, offset);
        }

        actions.Add(new DelayAction { OffsetTicks = offset, DurationTicks = ToTicks(plan.Interval) });

        return actions;
    }

    private long AppendClick(List<MacroAction> actions, MouseButton button, long offset)
    {
        actions.Add(new MouseButtonDownAction { OffsetTicks = offset, Button = button });
        offset += ToTicks(ClickHoldDuration);
        actions.Add(new MouseButtonUpAction { OffsetTicks = offset, Button = button });
        return offset;
    }

    private long ToTicks(TimeSpan duration) => (long)(duration.TotalSeconds * _clock.Frequency);
}
