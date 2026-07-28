using System.Collections.Concurrent;
using System.Threading.Channels;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Services;

public sealed class MacroRecorder
{
    private readonly IInputCaptureProvider _captureProvider;
    private readonly IKeyboardCaptureProvider _keyboardCaptureProvider;
    private readonly IHighResolutionClock _clock;
    private readonly IDisplayService _displayService;
    private readonly MacroOptimizer _optimizer;
    private readonly IApplicationLogger _logger;

    private ConcurrentQueue<MacroAction>? _actions;
    private CancellationTokenSource? _pumpCts;
    private Task? _mousePumpTask;
    private Task? _keyboardPumpTask;
    private long _recordingStartTimestamp;
    private DateTime _startedAtUtc;
    private DisplaySnapshot? _displaySnapshot;
    private IReadOnlyCollection<ushort> _reservedKeyCodes = [];

    public MacroRecorder(
        IInputCaptureProvider captureProvider,
        IKeyboardCaptureProvider keyboardCaptureProvider,
        IHighResolutionClock clock,
        IDisplayService displayService,
        MacroOptimizer optimizer,
        IApplicationLogger logger)
    {
        _captureProvider = captureProvider;
        _keyboardCaptureProvider = keyboardCaptureProvider;
        _clock = clock;
        _displayService = displayService;
        _optimizer = optimizer;
        _logger = logger;
    }

    public RecordingState State { get; private set; } = RecordingState.Idle;

    // reservedKeyCodes: kayıt sırasında F6/F8/F9 gibi diğer global kısayolların ham VK kodları
    // (kaydı aç/kapa eden kısayolun kendisi HARİÇ; onun için StopAsync'teki son-an filtrelemesi
    // kullanılır). Bu kodlara ait basma/bırakma olayları, kaydın tamamı boyunca WH_KEYBOARD_LL
    // kancasından gelen ham akıştan gerçek zamanlı olarak süzülür — kullanıcı kayıt sırasında
    // yanlışlıkla (veya bilerek) başka bir uygulama kısayoluna basarsa bu, makro içeriğine karışmaz.
    public Task StartAsync(CancellationToken cancellationToken, IReadOnlyCollection<ushort>? reservedKeyCodes = null)
    {
        if (State is not (RecordingState.Idle or RecordingState.Faulted))
        {
            throw new InvalidOperationException($"Makro kaydedici zaten '{State}' durumunda çalışıyor.");
        }

        _actions = new ConcurrentQueue<MacroAction>();
        _displaySnapshot = _displayService.GetSnapshot();
        _startedAtUtc = DateTime.UtcNow;
        _recordingStartTimestamp = _clock.GetTimestamp();
        _reservedKeyCodes = reservedKeyCodes ?? [];
        _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // State, her iki kanca da başarıyla kurulana kadar Recording'e alınmaz; aksi hâlde
        // _captureProvider.Start() başarılı olup _keyboardCaptureProvider.Start() başarısız olursa
        // (ya da tam tersi), State "Recording" görünmeye devam eder ama aslında hiçbir pompa
        // çalışmıyordur ve fare kancası tek başına kurulu/yakalıyor ama okunmuyor olarak kalır.
        try
        {
            _captureProvider.Start();
            _keyboardCaptureProvider.Start();
        }
        catch
        {
            _captureProvider.Stop();
            _keyboardCaptureProvider.Stop();
            _pumpCts.Dispose();
            _pumpCts = null;
            _actions = null;
            throw;
        }

        State = RecordingState.Recording;
        _mousePumpTask = PumpMouseEventsAsync(_actions, _pumpCts.Token);
        _keyboardPumpTask = PumpKeyboardEventsAsync(_actions, _pumpCts.Token);

        _logger.LogInformation("Makro kaydı başladı.");
        return Task.CompletedTask;
    }

    // excludeTrailingKeyCodes: kaydı bir global kısayolla (ör. F7) durduran çağrılarda, o kısayolun
    // kendi tuş (ve varsa değiştirici) kodları geçirilir. WH_KEYBOARD_LL kancası bu fiziksel basışı
    // da yakaladığı için, aksi hâlde her kayıt "kendi durdurma tuşuyla" kirlenir (bkz. StopAsync
    // içindeki filtreleme). UI butonuyla durdurma bu parametreyi kullanmaz; zaten fiziksel bir tuş
    // basışı içermez.
    public async Task<Macro> StopAsync(IReadOnlyCollection<ushort>? excludeTrailingKeyCodes = null)
    {
        if (State != RecordingState.Recording)
        {
            throw new InvalidOperationException("Makro kaydedici çalışmıyor.");
        }

        State = RecordingState.Stopping;
        var stopRequestedTimestamp = _clock.GetTimestamp();
        _captureProvider.Stop();
        _keyboardCaptureProvider.Stop();
        _pumpCts?.Cancel();

        if (_mousePumpTask is not null)
        {
            await _mousePumpTask;
        }

        if (_keyboardPumpTask is not null)
        {
            await _keyboardPumpTask;
        }

        var durationTicks = Math.Max(0, _clock.GetTimestamp() - _recordingStartTimestamp);

        // Fare ve klavye olayları iki ayrı kancadan geldiği için zaman sırasına göre birleştirilir
        // (OrderBy kararlı bir sıralamadır; aynı OffsetTicks değerine sahip olaylarda yakalanma
        // sırası korunur).
        var mergedActions = _actions!.OrderBy(a => a.OffsetTicks).ToList();

        if (excludeTrailingKeyCodes is { Count: > 0 })
        {
            // Durdurma isteğinden hemen önceki kısa bir pencerede (StopAsync'in fiilen çağrılmasına
            // yol açan fiziksel tuş basışı ile WM_HOTKEY'in işlenmesi arasındaki gecikme payı) bu
            // kodlara ait basma/bırakma olayları makrodan çıkarılır. Kaydın daha erken bir noktasında
            // kullanıcının bilerek bastığı aynı tuşlar dokunulmadan kalır.
            var trailingWindowTicks = (long)(0.5 * _clock.Frequency);
            var cutoffOffsetTicks = Math.Max(0, stopRequestedTimestamp - trailingWindowTicks - _recordingStartTimestamp);

            mergedActions.RemoveAll(action =>
                action.OffsetTicks >= cutoffOffsetTicks &&
                action switch
                {
                    KeyDownAction down => excludeTrailingKeyCodes.Contains(down.KeyCode),
                    KeyUpAction up => excludeTrailingKeyCodes.Contains(up.KeyCode),
                    _ => false
                });
        }

        var macro = new Macro
        {
            Id = Guid.NewGuid(),
            Name = $"Makro {_startedAtUtc:yyyy-MM-dd HH:mm:ss}",
            SchemaVersion = 1,
            CreatedAtUtc = _startedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            DurationTicks = durationTicks,
            DisplaySnapshot = _displaySnapshot!,
            Actions = mergedActions
        };

        var optimized = _optimizer.Optimize(macro);

        _actions = null;
        _pumpCts?.Dispose();
        _pumpCts = null;
        _mousePumpTask = null;
        _keyboardPumpTask = null;
        State = RecordingState.Idle;

        _logger.LogInformation(
            "Makro kaydı tamamlandı: {0} ham eylem, optimize sonrası {1} eylem.",
            macro.Actions.Count, optimized.Actions.Count);

        return optimized;
    }

    private async Task PumpMouseEventsAsync(ConcurrentQueue<MacroAction> actions, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var raw in _captureProvider.Events.ReadAllAsync(cancellationToken))
            {
                if (raw.IsInjectedByApplication)
                {
                    continue;
                }

                var offsetTicks = Math.Max(0, raw.TimestampTicks - _recordingStartTimestamp);
                actions.Enqueue(ConvertToAction(raw, offsetTicks));
            }
        }
        catch (OperationCanceledException)
        {
            // StopAsync tarafından beklenen iptal. ChannelReader.WaitToReadAsync, token zaten iptal
            // edilmişse kanalda okunmayı bekleyen veri olsa bile hemen iptal istisnası fırlatabilir;
            // bu yüzden hook'un StopAsync'in Stop()+Cancel() çağrıları sırasında yazmış olabileceği
            // son olaylar burada tek seferlik, engellemeyen bir tahliye ile de okunur.
            DrainRemaining(_captureProvider.Events, actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makro kaydı sırasında beklenmeyen hata oluştu (fare).");
            State = RecordingState.Faulted;
        }
    }

    private async Task PumpKeyboardEventsAsync(ConcurrentQueue<MacroAction> actions, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var raw in _keyboardCaptureProvider.Events.ReadAllAsync(cancellationToken))
            {
                if (raw.IsInjectedByApplication || _reservedKeyCodes.Contains(raw.KeyCode))
                {
                    continue;
                }

                var offsetTicks = Math.Max(0, raw.TimestampTicks - _recordingStartTimestamp);
                actions.Enqueue(ConvertToAction(raw, offsetTicks));
            }
        }
        catch (OperationCanceledException)
        {
            // StopAsync tarafından beklenen iptal; bkz. PumpMouseEventsAsync'teki tahliye notu.
            DrainRemaining(_keyboardCaptureProvider.Events, actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makro kaydı sırasında beklenmeyen hata oluştu (klavye).");
            State = RecordingState.Faulted;
        }
    }

    private void DrainRemaining(ChannelReader<RawMouseEvent> reader, ConcurrentQueue<MacroAction> actions)
    {
        while (reader.TryRead(out var raw))
        {
            if (raw.IsInjectedByApplication)
            {
                continue;
            }

            var offsetTicks = Math.Max(0, raw.TimestampTicks - _recordingStartTimestamp);
            actions.Enqueue(ConvertToAction(raw, offsetTicks));
        }
    }

    private void DrainRemaining(ChannelReader<RawKeyboardEvent> reader, ConcurrentQueue<MacroAction> actions)
    {
        while (reader.TryRead(out var raw))
        {
            if (raw.IsInjectedByApplication || _reservedKeyCodes.Contains(raw.KeyCode))
            {
                continue;
            }

            var offsetTicks = Math.Max(0, raw.TimestampTicks - _recordingStartTimestamp);
            actions.Enqueue(ConvertToAction(raw, offsetTicks));
        }
    }

    private static MacroAction ConvertToAction(RawMouseEvent raw, long offsetTicks) => raw.EventType switch
    {
        RawMouseEventType.Move => new MouseMoveAction { OffsetTicks = offsetTicks, X = raw.X, Y = raw.Y },
        RawMouseEventType.LeftButtonDown => new MouseButtonDownAction { OffsetTicks = offsetTicks, Button = MouseButton.Left },
        RawMouseEventType.LeftButtonUp => new MouseButtonUpAction { OffsetTicks = offsetTicks, Button = MouseButton.Left },
        RawMouseEventType.RightButtonDown => new MouseButtonDownAction { OffsetTicks = offsetTicks, Button = MouseButton.Right },
        RawMouseEventType.RightButtonUp => new MouseButtonUpAction { OffsetTicks = offsetTicks, Button = MouseButton.Right },
        RawMouseEventType.MiddleButtonDown => new MouseButtonDownAction { OffsetTicks = offsetTicks, Button = MouseButton.Middle },
        RawMouseEventType.MiddleButtonUp => new MouseButtonUpAction { OffsetTicks = offsetTicks, Button = MouseButton.Middle },
        RawMouseEventType.Wheel => new MouseWheelAction { OffsetTicks = offsetTicks, Delta = raw.WheelDelta },
        _ => throw new NotSupportedException($"Bilinmeyen ham fare olayı türü: {raw.EventType}")
    };

    private static MacroAction ConvertToAction(RawKeyboardEvent raw, long offsetTicks) => raw.EventType switch
    {
        RawKeyboardEventType.KeyDown => new KeyDownAction { OffsetTicks = offsetTicks, KeyCode = raw.KeyCode },
        RawKeyboardEventType.KeyUp => new KeyUpAction { OffsetTicks = offsetTicks, KeyCode = raw.KeyCode },
        _ => throw new NotSupportedException($"Bilinmeyen ham klavye olayı türü: {raw.EventType}")
    };
}
