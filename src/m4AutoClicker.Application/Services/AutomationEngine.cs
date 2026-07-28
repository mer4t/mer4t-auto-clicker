using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Services;

// Auto clicker ve makro oynatıcı ayrı motorlar yerine bu paylaşılan motoru kullanır.
public sealed class AutomationEngine
{
    private readonly IInputInjector _inputInjector;
    private readonly IHighResolutionClock _clock;
    private readonly PlaybackScheduler _scheduler;
    private readonly IApplicationLogger _logger;
    private readonly HashSet<MouseButton> _pressedButtons = [];
    private readonly HashSet<ushort> _pressedKeys = [];

    private CancellationTokenSource? _runCts;
    private TaskCompletionSource<bool>? _pauseGate;
    private PlaybackState _activeState = PlaybackState.Idle;

    // AutoClickerService ve MacroPlayer bu paylaşılan motoru SendInputa çağıran, birbirinden
    // BAĞIMSIZ SemaphoreSlim kapıları ile sarar (aynı anda çalışmayı önlemek için); ancak bu
    // motorun kendisi yalnızca "State" alanını check-then-act ile okuyup yazıyordu, ki bu atomik
    // değildir. F6 (AutoClicker) ve F8 (Makro oynatma) neredeyse aynı anda tetiklenirse, iki thread
    // State'i "Idle" olarak görüp ikisi de RunAsync'e girebilir ve _pressedButtons/_pressedKeys gibi
    // paylaşılan alanları eşzamanlı olarak bozabilir. Bu int, RunAsync'e girişi gerçekten atomik
    // hâle getirir; State ise dışarıya durum raporlamak için ayrı olarak tutulmaya devam eder.
    private int _isRunning;

    // PauseAsync/ResumeAsync ile RunAsync'in tamamlanma anındaki State ataması arasındaki dar
    // pencereyi (son eylem bitip State henüz Idle'a çekilmeden önce Pause çağrılırsa State'in
    // yarım kalmış/tutarsız bir şekilde üzerine yazılması) önlemek için State geçişleri bu kilit
    // altında yapılır.
    private readonly object _stateLock = new();

    public AutomationEngine(
        IInputInjector inputInjector,
        IHighResolutionClock clock,
        PlaybackScheduler scheduler,
        IApplicationLogger logger)
    {
        _inputInjector = inputInjector;
        _clock = clock;
        _scheduler = scheduler;
        _logger = logger;
    }

    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    public async Task<PlaybackResult> RunAsync(
        IReadOnlyList<MacroAction> actions,
        PlaybackOptions options,
        PlaybackState activeState,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException($"Otomasyon motoru zaten '{State}' durumunda çalışıyor.");
        }

        try
        {
            if (actions.Count == 0)
            {
                State = PlaybackState.Idle;
                return new PlaybackResult { Success = true, ExecutedActionCount = 0 };
            }

            _pressedButtons.Clear();
            _pressedKeys.Clear();
            _pauseGate = null;
            _activeState = activeState;
            State = activeState;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runCts = linkedCts;

            var executedTotal = 0;

            try
            {
                if (options.StartDelay > TimeSpan.Zero)
                {
                    var startDelayTarget = _clock.GetTimestamp() + (long)(options.StartDelay.TotalSeconds * _clock.Frequency);
                    await _scheduler.WaitUntilAsync(startDelayTarget, linkedCts.Token);
                    // Başlangıç gecikmesi sırasında duraklatılmış olabilir; devam etmeden önce bekler.
                    // (İlk eylemin kendi duraklatma kontrolü zaten güvenliği sağlıyordu; bu, uzun bir
                    // başlangıç gecikmesinde duraklatmanın süre dolana kadar beklemeden hemen etkili
                    // olmasını sağlayan bir yanıt verme iyileştirmesidir.)
                    await WaitWhilePausedAsync(linkedCts.Token);
                    linkedCts.Token.ThrowIfCancellationRequested();
                }

                var iteration = 0;
                while (true)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();

                    if (options.RepeatMode == RepeatMode.FixedCount && iteration >= (options.RepeatCount ?? 1))
                    {
                        break;
                    }

                    executedTotal += await RunIterationAsync(actions, options.SpeedMultiplier, linkedCts.Token);
                    iteration++;
                }

                CompleteRun(PlaybackState.Idle);
                return new PlaybackResult { Success = true, ExecutedActionCount = executedTotal };
            }
            catch (OperationCanceledException)
            {
                ReleaseAllPressedButtons();
                CompleteRun(PlaybackState.Idle);
                return new PlaybackResult { Success = true, ExecutedActionCount = executedTotal };
            }
            catch (Exception ex)
            {
                ReleaseAllPressedButtons();
                CompleteRun(PlaybackState.Faulted);
                _logger.LogError(ex, "Otomasyon motorunda beklenmeyen hata oluştu.");
                return new PlaybackResult { Success = false, ExecutedActionCount = executedTotal, ErrorMessage = ex.Message };
            }
            finally
            {
                _runCts = null;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    // RunAsync tamamlanırken çağrılır: State'i belirtilen son duruma geçirir ve olası bir
    // _pauseGate'i temizler. Bu, PauseAsync ile aynı kilit altında yapılır; aksi hâlde RunAsync'in
    // son eylemi bitirip bu satırlara ulaşması ile PauseAsync'in dar bir pencerede araya girip
    // State'i Paused'a çekmesi arasında yarışılabilir ve sahipsiz (hiçbir zaman çözülmeyecek) bir
    // _pauseGate ile State "Paused" görünürken motorun aslında durmuş olduğu bir tutarsızlık oluşabilir.
    private void CompleteRun(PlaybackState finalState)
    {
        lock (_stateLock)
        {
            _pauseGate = null;
            State = finalState;
        }
    }

    public Task PauseAsync()
    {
        lock (_stateLock)
        {
            if (State is PlaybackState.Clicking or PlaybackState.Playing)
            {
                _pauseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                State = PlaybackState.Paused;
            }
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        lock (_stateLock)
        {
            if (State == PlaybackState.Paused)
            {
                State = _activeState;
                _pauseGate?.TrySetResult(true);
                _pauseGate = null;
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _runCts?.Cancel();
        _pauseGate?.TrySetResult(true);
        return Task.CompletedTask;
    }

    private async Task<int> RunIterationAsync(
        IReadOnlyList<MacroAction> actions, double speedMultiplier, CancellationToken cancellationToken)
    {
        var iterationStart = _clock.GetTimestamp();
        // Duraklatma sırasında geçen gerçek süre buraya eklenir; böylece resume sonrası kalan
        // eylemler, duraklatma hiç yaşanmamış gibi aralarındaki göreli zamanlamayı korur (bir
        // "yakalama" patlaması yaşanmaz).
        var pausedTicksAccumulated = 0L;
        var executed = 0;

        foreach (var action in actions)
        {
            // DelayAction için hedef zaman, kendi süresi kadar ileri kaydırılır; diğer eylemler anlık noktalardır.
            var effectiveOffsetTicks = action is DelayAction delay
                ? delay.OffsetTicks + delay.DurationTicks
                : action.OffsetTicks;

            var targetTimestamp = iterationStart + pausedTicksAccumulated + ScaleTicks(effectiveOffsetTicks, speedMultiplier);
            await _scheduler.WaitUntilAsync(targetTimestamp, cancellationToken);
            pausedTicksAccumulated += await WaitWhilePausedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            ExecuteAction(action);
            executed++;
        }

        return executed;
    }

    // Duraklatılmışsa resume'a kadar bekler ve bu bekleyişte geçen gerçek süreyi (tick cinsinden)
    // döndürür; duraklatılmamışsa 0 döner.
    private async Task<long> WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        var gate = _pauseGate;
        if (gate is null)
        {
            return 0;
        }

        var pauseStartedAt = _clock.GetTimestamp();

        await using (cancellationToken.UnsafeRegister(static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(), gate).ConfigureAwait(false))
        {
            await gate.Task.ConfigureAwait(false);
        }

        return _clock.GetTimestamp() - pauseStartedAt;
    }

    private void ExecuteAction(MacroAction action)
    {
        switch (action)
        {
            case MouseMoveAction move:
                ThrowIfFailed(_inputInjector.MoveMouse(new ScreenPoint(move.X, move.Y)));
                break;
            case MouseButtonDownAction down:
                ThrowIfFailed(_inputInjector.MouseDown(down.Button));
                _pressedButtons.Add(down.Button);
                break;
            case MouseButtonUpAction up:
                ThrowIfFailed(_inputInjector.MouseUp(up.Button));
                _pressedButtons.Remove(up.Button);
                break;
            case MouseWheelAction wheel:
                ThrowIfFailed(_inputInjector.Scroll(wheel.Delta));
                break;
            case KeyDownAction keyDown:
                ThrowIfFailed(_inputInjector.KeyDown(keyDown.KeyCode));
                _pressedKeys.Add(keyDown.KeyCode);
                break;
            case KeyUpAction keyUp:
                ThrowIfFailed(_inputInjector.KeyUp(keyUp.KeyCode));
                _pressedKeys.Remove(keyUp.KeyCode);
                break;
            case DelayAction:
                break;
            default:
                throw new NotSupportedException($"Bilinmeyen eylem türü: {action.GetType().Name}");
        }
    }

    private static void ThrowIfFailed(InputInjectionResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Input enjeksiyonu başarısız oldu.");
        }
    }

    private void ReleaseAllPressedButtons()
    {
        foreach (var button in _pressedButtons.ToArray())
        {
            try
            {
                _inputInjector.MouseUp(button);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Basılı fare tuşu bırakılırken hata oluştu: {0}", ex.Message);
            }
        }

        _pressedButtons.Clear();

        foreach (var keyCode in _pressedKeys.ToArray())
        {
            try
            {
                _inputInjector.KeyUp(keyCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Basılı tuş bırakılırken hata oluştu: {0}", ex.Message);
            }
        }

        _pressedKeys.Clear();
    }

    private static long ScaleTicks(long offsetTicks, double speedMultiplier)
    {
        if (speedMultiplier <= 0)
        {
            speedMultiplier = 1.0;
        }

        return (long)(offsetTicks / speedMultiplier);
    }
}
