using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.Application.Tests.Fakes;

// Gerçek RegisterHotKey çağırmadan WindowsHotkeyService'in beklenen sözleşmesini taklit eder:
// aynı kimlik veya aynı kombinasyon reddedilir, native reddi simüle edebilir.
public sealed class FakeHotkeyService : IHotkeyService
{
    private readonly Dictionary<string, HotkeyDefinition> _registered = new();
    private readonly HashSet<(VirtualKey Key, HotkeyModifiers Modifiers)> _combinations = new();

    public bool Started { get; private set; }

    public int DisposeCallCount { get; private set; }

    public int UnregisterAllCallCount { get; private set; }

    public HashSet<string> HotkeyIdsToReject { get; } = [];

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Start() => Started = true;

    public HotkeyRegistrationResult Register(HotkeyDefinition hotkey)
    {
        if (_registered.ContainsKey(hotkey.Id))
        {
            return HotkeyRegistrationResult.Failed(
                hotkey.Id, HotkeyRegistrationErrorType.AlreadyRegistered, "Zaten kayıtlı.");
        }

        var combination = (hotkey.Key, hotkey.Modifiers);
        if (_combinations.Contains(combination))
        {
            return HotkeyRegistrationResult.Failed(
                hotkey.Id, HotkeyRegistrationErrorType.CombinationAlreadyUsed, "Kombinasyon zaten kullanılıyor.");
        }

        if (HotkeyIdsToReject.Contains(hotkey.Id))
        {
            return HotkeyRegistrationResult.Failed(
                hotkey.Id, HotkeyRegistrationErrorType.RegistrationRejected, "Başka bir uygulama kullanıyor.", nativeErrorCode: 1409);
        }

        _registered[hotkey.Id] = hotkey;
        _combinations.Add(combination);
        return HotkeyRegistrationResult.Successful(hotkey.Id);
    }

    public bool Unregister(string hotkeyId)
    {
        if (!_registered.TryGetValue(hotkeyId, out var definition))
        {
            return false;
        }

        _registered.Remove(hotkeyId);
        _combinations.Remove((definition.Key, definition.Modifiers));
        return true;
    }

    public void UnregisterAll()
    {
        UnregisterAllCallCount++;
        foreach (var id in _registered.Keys.ToArray())
        {
            Unregister(id);
        }
    }

    public void RaiseHotkeyPressed(string hotkeyId) =>
        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs { HotkeyId = hotkeyId });

    public void Dispose()
    {
        DisposeCallCount++;
        UnregisterAll();
    }
}
