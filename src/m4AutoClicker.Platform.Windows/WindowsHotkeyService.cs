using System.Runtime.InteropServices;
using System.Windows.Interop;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Hotkeys;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace m4AutoClicker.Platform.Windows;

// F6-F9 gibi global kısayolları RegisterHotKey/UnregisterHotKey ile OS seviyesinde kaydeder.
// WM_HOTKEY mesajlarını almak için kendine ait, görünmez bir mesaj-only HwndSource penceresi
// oluşturur; bu sayede WPF ana penceresinin yaşam döngüsüne bağımlı kalmaz.
public sealed class WindowsHotkeyService : IHotkeyService
{
    private const int WmHotkey = (int)PInvoke.WM_HOTKEY;

    private readonly IApplicationLogger _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _nativeIdsByHotkeyId = new();
    private readonly Dictionary<int, string> _hotkeyIdsByNativeId = new();
    private readonly Dictionary<string, HotkeyDefinition> _definitionsByHotkeyId = new();
    private readonly HashSet<(VirtualKey Key, HotkeyModifiers Modifiers)> _registeredCombinations = new();

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle = IntPtr.Zero;
    private int _nextNativeId = 1;
    private bool _disposed;

    public WindowsHotkeyService(IApplicationLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WindowsHotkeyService));
            }

            if (_hwndSource is not null)
            {
                _logger.LogWarning("WindowsHotkeyService.Start birden fazla kez çağrıldı, yok sayılıyor.");
                return;
            }

            // HWND_MESSAGE (-3): görünmez, mesaj-only bir pencere oluşturur. Bu pencere hiçbir zaman
            // ekranda görünmez; yalnızca WM_HOTKEY gibi thread mesajlarını almak için kullanılır.
            var parameters = new HwndSourceParameters("m4AutoClicker.HotkeyMessageSink")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0,
                ParentWindow = new IntPtr(-3)
            };

            var source = new HwndSource(parameters);
            source.AddHook(WndProc);
            _hwndSource = source;
            _windowHandle = source.Handle;

            _logger.LogInformation("Global hotkey mesaj penceresi oluşturuldu. Handle: {0}", _windowHandle);
        }
    }

    public HotkeyRegistrationResult Register(HotkeyDefinition hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);

        _logger.LogInformation("Hotkey kayıt denemesi: {0} ({1}+{2}).", hotkey.Id, hotkey.Modifiers, hotkey.Key);

        lock (_gate)
        {
            if (_disposed)
            {
                return Fail(hotkey.Id, HotkeyRegistrationErrorType.Unknown, "Hotkey servisi dispose edilmiş.");
            }

            if (_windowHandle == IntPtr.Zero || _hwndSource is null)
            {
                return Fail(
                    hotkey.Id,
                    HotkeyRegistrationErrorType.WindowHandleUnavailable,
                    "Pencere handle'ı henüz hazır değil. Hotkey kaydı yapılamadı.");
            }

            if (_nativeIdsByHotkeyId.ContainsKey(hotkey.Id))
            {
                return Fail(
                    hotkey.Id,
                    HotkeyRegistrationErrorType.AlreadyRegistered,
                    $"'{hotkey.Id}' kimlikli hotkey zaten kayıtlı.");
            }

            var combination = (hotkey.Key, hotkey.Modifiers);
            if (_registeredCombinations.Contains(combination))
            {
                return Fail(
                    hotkey.Id,
                    HotkeyRegistrationErrorType.CombinationAlreadyUsed,
                    "Bu tuş kombinasyonu uygulama içinde başka bir kısayol tarafından kullanılıyor.");
            }

            var nativeId = _nextNativeId++;
            var modifiers = ToWin32Modifiers(hotkey.Modifiers) | HOT_KEY_MODIFIERS.MOD_NOREPEAT;

            var success = PInvoke.RegisterHotKey(new HWND(_windowHandle), nativeId, modifiers, (uint)hotkey.Key);
            if (!success)
            {
                var nativeError = Marshal.GetLastPInvokeError();
                _logger.LogWarning(
                    "Hotkey kaydı başarısız: {0} ({1}+{2}). Native hata kodu: {3}",
                    hotkey.Id, hotkey.Modifiers, hotkey.Key, nativeError);

                return Fail(
                    hotkey.Id,
                    HotkeyRegistrationErrorType.RegistrationRejected,
                    $"{DescribeCombination(hotkey)} global kısayolu kaydedilemedi. Bu tuş başka bir uygulama tarafından kullanılıyor olabilir.",
                    nativeError);
            }

            _nativeIdsByHotkeyId[hotkey.Id] = nativeId;
            _hotkeyIdsByNativeId[nativeId] = hotkey.Id;
            _definitionsByHotkeyId[hotkey.Id] = hotkey;
            _registeredCombinations.Add(combination);

            _logger.LogInformation("Hotkey kaydedildi: {0} -> native id {1}.", hotkey.Id, nativeId);
            return HotkeyRegistrationResult.Successful(hotkey.Id);
        }
    }

    public bool Unregister(string hotkeyId)
    {
        lock (_gate)
        {
            return UnregisterInternal(hotkeyId);
        }
    }

    public void UnregisterAll()
    {
        lock (_gate)
        {
            foreach (var hotkeyId in _nativeIdsByHotkeyId.Keys.ToArray())
            {
                UnregisterInternal(hotkeyId);
            }
        }

        _logger.LogInformation("Tüm global hotkey kayıtları kaldırıldı.");
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

        UnregisterAll();

        lock (_gate)
        {
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
                _windowHandle = IntPtr.Zero;
                _logger.LogInformation("Global hotkey mesaj penceresi kapatıldı.");
            }
        }
    }

    private bool UnregisterInternal(string hotkeyId)
    {
        if (!_nativeIdsByHotkeyId.TryGetValue(hotkeyId, out var nativeId))
        {
            return false;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            if (!PInvoke.UnregisterHotKey(new HWND(_windowHandle), nativeId))
            {
                var nativeError = Marshal.GetLastPInvokeError();
                _logger.LogWarning(
                    "UnregisterHotKey başarısız oldu: {0} (native hata kodu: {1}).", hotkeyId, nativeError);
            }
        }

        _nativeIdsByHotkeyId.Remove(hotkeyId);
        _hotkeyIdsByNativeId.Remove(nativeId);

        if (_definitionsByHotkeyId.Remove(hotkeyId, out var definition))
        {
            _registeredCombinations.Remove((definition.Key, definition.Modifiers));
        }

        _logger.LogInformation("Hotkey kaydı kaldırıldı: {0}.", hotkeyId);
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var nativeId = wParam.ToInt32();

        string? hotkeyId;
        lock (_gate)
        {
            _hotkeyIdsByNativeId.TryGetValue(nativeId, out hotkeyId);
        }

        if (hotkeyId is null)
        {
            // Bu uygulamada kayıtlı olmayan bir native ID; yok sayılır.
            return IntPtr.Zero;
        }

        _logger.LogInformation("Global hotkey tetiklendi: {0}.", hotkeyId);
        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs { HotkeyId = hotkeyId });
        handled = true;
        return IntPtr.Zero;
    }

    private static HotkeyRegistrationResult Fail(
        string hotkeyId, HotkeyRegistrationErrorType errorType, string message, int? nativeErrorCode = null) =>
        HotkeyRegistrationResult.Failed(hotkeyId, errorType, message, nativeErrorCode);

    private static string DescribeCombination(HotkeyDefinition hotkey) => HotkeyLabelFormatter.Format(hotkey);

    // Domain.HotkeyModifiers bit değerleri Win32 MOD_* sabitleriyle eşleşecek şekilde tasarlandı (bkz. VirtualKey).
    private static HOT_KEY_MODIFIERS ToWin32Modifiers(HotkeyModifiers modifiers) => (HOT_KEY_MODIFIERS)(uint)modifiers;
}
