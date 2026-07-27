using System.Threading.Channels;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MertClicker.Platform.Windows;

// WH_MOUSE_LL global düşük seviye fare kancası ile sistem genelindeki fare olaylarını yakalar.
// Hook callback'i işletim sistemi tarafından çok kısa sürede dönmesi beklenen bir çağrı olduğu için
// burada yalnızca ham veriyi bir Channel'a yazar; ağır işleme (MacroRecorder tarafında) ayrı bir
// tüketici döngüsünde yapılır.
public sealed class WindowsMouseHook : IInputCaptureProvider
{
    private readonly IHighResolutionClock _clock;
    private readonly IApplicationLogger _logger;
    private readonly Channel<RawMouseEvent> _channel = Channel.CreateUnbounded<RawMouseEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private readonly object _gate = new();

    private HOOKPROC? _hookProc;
    private UnhookWindowsHookExSafeHandle? _hookHandle;
    private bool _disposed;

    public WindowsMouseHook(IHighResolutionClock clock, IApplicationLogger logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public bool IsCapturing { get; private set; }

    public ChannelReader<RawMouseEvent> Events => _channel.Reader;

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
                throw new ObjectDisposedException(nameof(WindowsMouseHook));
            }

            if (IsCapturing)
            {
                _logger.LogWarning("WindowsMouseHook.Start birden fazla kez çağrıldı, yok sayılıyor.");
                return;
            }

            // Delege, native kod tarafından tutulan bir fonksiyon işaretçisine dönüştürüldüğü için GC'ye
            // karşı canlı tutulmalı; bu yüzden alan (field) olarak saklanıyor.
            _hookProc = HookCallback;

            var moduleHandle = PInvoke.GetModuleHandle((string?)null);
            _hookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _hookProc, moduleHandle, 0);

            if (_hookHandle is null || _hookHandle.IsInvalid)
            {
                _hookProc = null;
                _logger.LogError(null, "SetWindowsHookEx (WH_MOUSE_LL) başarısız oldu.");
                throw new InvalidOperationException("Global fare kancası kurulamadı.");
            }

            IsCapturing = true;
            _logger.LogInformation("Global fare kancası (WH_MOUSE_LL) kuruldu.");
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

            _logger.LogInformation("Global fare kancası kaldırıldı.");
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

    private unsafe bool TryDecode(int nCode, WPARAM wParam, LPARAM lParam, out RawMouseEvent rawEvent)
    {
        rawEvent = default!;

        RawMouseEventType? eventType = (uint)wParam.Value switch
        {
            PInvoke.WM_MOUSEMOVE => RawMouseEventType.Move,
            PInvoke.WM_LBUTTONDOWN => RawMouseEventType.LeftButtonDown,
            PInvoke.WM_LBUTTONUP => RawMouseEventType.LeftButtonUp,
            PInvoke.WM_RBUTTONDOWN => RawMouseEventType.RightButtonDown,
            PInvoke.WM_RBUTTONUP => RawMouseEventType.RightButtonUp,
            PInvoke.WM_MBUTTONDOWN => RawMouseEventType.MiddleButtonDown,
            PInvoke.WM_MBUTTONUP => RawMouseEventType.MiddleButtonUp,
            PInvoke.WM_MOUSEWHEEL => RawMouseEventType.Wheel,
            _ => null
        };

        if (eventType is null)
        {
            // WM_MOUSEHWHEEL, WM_NCMOUSEMOVE gibi ilgilenmediğimiz mesajlar; yok sayılır.
            return false;
        }

        var data = (MSLLHOOKSTRUCT*)lParam.Value;
        var wheelDelta = eventType == RawMouseEventType.Wheel ? (short)(data->mouseData >> 16) : 0;
        var isInjectedByApplication = data->dwExtraInfo == ApplicationInputMarker.Signature;

        rawEvent = new RawMouseEvent
        {
            EventType = eventType.Value,
            X = data->pt.X,
            Y = data->pt.Y,
            WheelDelta = wheelDelta,
            TimestampTicks = _clock.GetTimestamp(),
            IsInjectedByApplication = isInjectedByApplication
        };

        return true;
    }
}
