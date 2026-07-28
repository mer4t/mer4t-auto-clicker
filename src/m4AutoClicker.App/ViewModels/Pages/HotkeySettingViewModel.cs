using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.App.ViewModels.Pages;

// Ayarlar ekranındaki bir satırın (bir eylem için düzenlenebilir kısayol) durumu.
public sealed partial class HotkeySettingViewModel : ObservableObject
{
    public required string HotkeyId { get; init; }

    public required string Label { get; init; }

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
    private bool _isListening;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public string DisplayText => IsListening ? "Tuşlara basın… (Esc: iptal)" : ComboText;

    private string ComboText
    {
        get
        {
            var builder = new StringBuilder();
            if (UseCtrl) builder.Append("Ctrl+");
            if (UseAlt) builder.Append("Alt+");
            if (UseShift) builder.Append("Shift+");
            if (UseWin) builder.Append("Win+");
            builder.Append(SelectedKey);
            return builder.ToString();
        }
    }

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

    // Klavye yakalama sırasında kaydedilen kombinasyonu uygular ve dinlemeyi sonlandırır.
    public void ApplyCaptured(VirtualKey key, HotkeyModifiers modifiers)
    {
        SelectedKey = key;
        UseCtrl = modifiers.HasFlag(HotkeyModifiers.Control);
        UseAlt = modifiers.HasFlag(HotkeyModifiers.Alt);
        UseShift = modifiers.HasFlag(HotkeyModifiers.Shift);
        UseWin = modifiers.HasFlag(HotkeyModifiers.Windows);
        IsListening = false;
    }

    partial void OnSelectedKeyChanged(VirtualKey value) => OnPropertyChanged(nameof(DisplayText));

    partial void OnUseCtrlChanged(bool value) => OnPropertyChanged(nameof(DisplayText));

    partial void OnUseAltChanged(bool value) => OnPropertyChanged(nameof(DisplayText));

    partial void OnUseShiftChanged(bool value) => OnPropertyChanged(nameof(DisplayText));

    partial void OnUseWinChanged(bool value) => OnPropertyChanged(nameof(DisplayText));

    partial void OnIsListeningChanged(bool value) => OnPropertyChanged(nameof(DisplayText));
}
