namespace MertClicker.Domain.Hotkeys;

public static class HotkeyDefaults
{
    public static IReadOnlyList<HotkeyDefinition> All { get; } =
    [
        new HotkeyDefinition { Id = HotkeyIds.AutoClickerToggle, Key = VirtualKey.F6 },
        new HotkeyDefinition { Id = HotkeyIds.MacroRecorderToggle, Key = VirtualKey.F7 },
        new HotkeyDefinition { Id = HotkeyIds.MacroPlaybackToggle, Key = VirtualKey.F8 },
        new HotkeyDefinition { Id = HotkeyIds.EmergencyStop, Key = VirtualKey.F9 }
    ];
}
