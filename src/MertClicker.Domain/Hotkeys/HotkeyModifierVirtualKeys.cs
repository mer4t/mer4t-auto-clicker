namespace MertClicker.Domain.Hotkeys;

// WH_KEYBOARD_LL gibi düşük seviye kancalar, değiştirici tuşlar için genel (VK_CONTROL) veya
// sol/sağ özel (VK_LCONTROL/VK_RCONTROL) sanal tuş kodlarından herhangi birini bildirebilir; bu
// yüzden bir HotkeyModifiers bayrağına karşılık gelen TÜM olası ham kodlar döndürülür.
public static class HotkeyModifierVirtualKeys
{
    public static IEnumerable<ushort> For(HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            yield return 0x12; // VK_MENU
            yield return 0xA4; // VK_LMENU
            yield return 0xA5; // VK_RMENU
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            yield return 0x11; // VK_CONTROL
            yield return 0xA2; // VK_LCONTROL
            yield return 0xA3; // VK_RCONTROL
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            yield return 0x10; // VK_SHIFT
            yield return 0xA0; // VK_LSHIFT
            yield return 0xA1; // VK_RSHIFT
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            yield return 0x5B; // VK_LWIN
            yield return 0x5C; // VK_RWIN
        }
    }
}
