using System.Threading.Channels;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MertClicker.Platform.Windows;

// WH_KEYBOARD_LL global düşük seviye klavye kancası ile sistem genelindeki tuş olaylarını yakalar.
// WindowsMouseHook ile aynı yaklaşım: hook callback'i yalnızca ham veriyi bir Channel'a yazar, ağır
// işleme (MacroRecorder tarafında) ayrı bir tüketici döngüsünde yapılır.
public sealed class WindowsKeyboardHook : IKeyboardCaptureProvider
{
    private readonly IHighResolutionClock _clock;
    private readonly IApplicationLogger _logger;
    private readonly Channel<RawKeyboardEvent> _channel = Channel.CreateUnbounded<RawKeyboardEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private readonly object _gate = new();

    private HOOKPROC? _hookProc;
    private UnhookWindowsHookExSafeHandle? _hookHandle;
    private bool _disposed;

    public WindowsKeyboardHook(IHighResolutionClock clock, IApplicationLogger logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public bool IsCapturing { get; private set; }

    public ChannelReader<RawKeyboardEvent> Events => _channel.Reader;

    public void Start()
    {
        // Start/Stop/Dispose, bu sınıf DI'da singleton olarak kaydedildiği ve birden fazla thread'den
        // (ör. UI thread'i ile hotkey/acil durdurma yolu) çağrılabildiği için birbirlerine karşı
        // korunur; aksi hâlde iki eşzamanlı Start() çağrısı, ilkinin GC'ye karşı canlı tutulması
        // gereken _hookProc delegesini ikincisininkiyle üzerine yazıp dangling bir native fonksiyon
        // işaretçisi bırakabilir.
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WindowsKeyboardHook));
            }

            if (IsCapturing)
            {
                _logger.LogWarning("WindowsKeyboardHook.Start birden fazla kez çağrıldı, yok sayılıyor.");
                return;
            }

            // Delege, native kod tarafından tutulan bir fonksiyon işaretçisine dönüştürüldüğü için GC'ye
            // karşı canlı tutulmalı; bu yüzden alan (field) olarak saklanıyor.
            _hookProc = HookCallback;

            var moduleHandle = PInvoke.GetModuleHandle((string?)null);
            _hookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);

            if (_hookHandle is null || _hookHandle.IsInvalid)
            {
                _hookProc = null;
                _logger.LogError(null, "SetWindowsHookEx (WH_KEYBOARD_LL) başarısız oldu.");
                throw new InvalidOperationException("Global klavye kancası kurulamadı.");
            }

            IsCapturing = true;
            _logger.LogInformation("Global klavye kancası (WH_KEYBOARD_LL) kuruldu.");
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsCapturing)
            {
                return;
            }

            _hookHandle?.Dispose();
            _hookHandle = null;
            _hookProc = null;
            IsCapturing = false;

            _logger.LogInformation("Global klavye kancası kaldırıldı.");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
        _channel.Writer.TryComplete();
    }

    private unsafe LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && TryDecode(nCode, wParam, lParam, out var rawEvent))
        {
            // Non-blocking: unbounded kanala yazma her zaman anında tamamlanır, hook'u geciktirmez.
            _channel.Writer.TryWrite(rawEvent);
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    private unsafe bool TryDecode(int nCode, WPARAM wParam, LPARAM lParam, out RawKeyboardEvent rawEvent)
    {
        rawEvent = default!;

        RawKeyboardEventType? eventType = (uint)wParam.Value switch
        {
            PInvoke.WM_KEYDOWN => RawKeyboardEventType.KeyDown,
            PInvoke.WM_SYSKEYDOWN => RawKeyboardEventType.KeyDown,
            PInvoke.WM_KEYUP => RawKeyboardEventType.KeyUp,
            PInvoke.WM_SYSKEYUP => RawKeyboardEventType.KeyUp,
            _ => null
        };

        if (eventType is null)
        {
            return false;
        }

        var data = (KBDLLHOOKSTRUCT*)lParam.Value;
        var isInjectedByApplication = data->dwExtraInfo == ApplicationInputMarker.Signature;

        rawEvent = new RawKeyboardEvent
        {
            EventType = eventType.Value,
            KeyCode = (ushort)data->vkCode,
            TimestampTicks = _clock.GetTimestamp(),
            IsInjectedByApplication = isInjectedByApplication
        };

        return true;
    }
}
