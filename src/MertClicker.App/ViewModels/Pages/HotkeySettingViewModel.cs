using CommunityToolkit.Mvvm.ComponentModel;
using MertClicker.Domain.Hotkeys;

namespace MertClicker.App.ViewModels.Pages;

// Ayarlar ekranındaki bir satırın (bir eylem için düzenlenebilir kısayol) durumu.
public sealed partial class HotkeySettingViewModel : ObservableObject
{
    public required string HotkeyId { get; init; }

    public required string Label { get; init; }

    public IReadOnlyList<VirtualKey> KeyOptions { get; } = Enum.GetValues<VirtualKey>();

    [ObservableProperty]
    private VirtualKey _selectedKey;

    [ObservableProperty]
    private bool _useCtrl;

    [ObservableProperty]
    private bool _useAlt;

    [ObservableProperty]
    private bool _useShift;

    [ObservableProperty]
    private bool _useWin;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public HotkeyDefinition ToDefinition() => new()
    {
        Id = HotkeyId,
        Key = SelectedKey,
        Modifiers = (UseCtrl ? HotkeyModifiers.Control : HotkeyModifiers.None)
            | (UseAlt ? HotkeyModifiers.Alt : HotkeyModifiers.None)
            | (UseShift ? HotkeyModifiers.Shift : HotkeyModifiers.None)
            | (UseWin ? HotkeyModifiers.Windows : HotkeyModifiers.None)
    };

    public void ApplyFrom(HotkeyDefinition definition)
    {
        SelectedKey = definition.Key;
        UseCtrl = definition.Modifiers.HasFlag(HotkeyModifiers.Control);
        UseAlt = definition.Modifiers.HasFlag(HotkeyModifiers.Alt);
        UseShift = definition.Modifiers.HasFlag(HotkeyModifiers.Shift);
        UseWin = definition.Modifiers.HasFlag(HotkeyModifiers.Windows);
    }
}
