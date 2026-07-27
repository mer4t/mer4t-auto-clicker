using MertClicker.Application.Models;
using MertClicker.Domain.Hotkeys;

namespace MertClicker.Application.Abstractions;

public interface IHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    // Global kısayol mesajlarını alabilmek için platforma özel mesaj alıcısını hazırlar.
    void Start();

    HotkeyRegistrationResult Register(HotkeyDefinition hotkey);

    bool Unregister(string hotkeyId);

    void UnregisterAll();
}
