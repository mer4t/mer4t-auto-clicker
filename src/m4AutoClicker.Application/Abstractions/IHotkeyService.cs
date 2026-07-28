using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.Application.Abstractions;

public interface IHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    // Global kısayol mesajlarını alabilmek için platforma özel mesaj alıcısını hazırlar.
    void Start();

    HotkeyRegistrationResult Register(HotkeyDefinition hotkey);

    bool Unregister(string hotkeyId);

    void UnregisterAll();
}
