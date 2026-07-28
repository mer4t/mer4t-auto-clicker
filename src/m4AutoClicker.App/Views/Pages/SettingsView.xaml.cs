using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using m4AutoClicker.App.ViewModels.Pages;
using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.App.Views.Pages;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void HotkeyCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var row = (HotkeySettingViewModel)button.DataContext;

        if (DataContext is SettingsViewModel viewModel)
        {
            foreach (var binding in viewModel.HotkeyBindings)
            {
                binding.IsListening = false;
            }
        }

        row.StatusText = string.Empty;
        row.IsListening = true;
        button.Focus();
    }

    private async void HotkeyCaptureButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var row = (HotkeySettingViewModel)((Button)sender).DataContext;
        if (!row.IsListening)
        {
            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            row.IsListening = false;
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        if (!TryMapToVirtualKey(key, out var virtualKey))
        {
            row.StatusText = "Desteklenmeyen tuş: yalnızca F1-F12 kullanılabilir.";
            return;
        }

        var modifiers = (HotkeyModifiers)(int)Keyboard.Modifiers;
        row.ApplyCaptured(virtualKey, modifiers);

        // Bir kısayolu yakalamak, ayrı bir "Kaydet" tıklaması gerektirmeden native kaydı hemen
        // günceller: eski tuş anında bırakılır, yeni tuş anında kaydedilir ve diske yazılır.
        // Önceki tasarımda kullanıcı yakalamadan sonra "Kaydet"e basmayı unutursa arayüz yeni
        // tuşu gösterse bile gerçek global hotkey eski tuşta kalıyordu.
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.SaveCommand.ExecuteAsync(null);
        }
    }

    private void HotkeyCaptureButton_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var row = (HotkeySettingViewModel)((Button)sender).DataContext;
        row.IsListening = false;
    }

    private static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;

    private static bool TryMapToVirtualKey(Key key, out VirtualKey virtualKey)
    {
        switch (key)
        {
            case Key.F1: virtualKey = VirtualKey.F1; return true;
            case Key.F2: virtualKey = VirtualKey.F2; return true;
            case Key.F3: virtualKey = VirtualKey.F3; return true;
            case Key.F4: virtualKey = VirtualKey.F4; return true;
            case Key.F5: virtualKey = VirtualKey.F5; return true;
            case Key.F6: virtualKey = VirtualKey.F6; return true;
            case Key.F7: virtualKey = VirtualKey.F7; return true;
            case Key.F8: virtualKey = VirtualKey.F8; return true;
            case Key.F9: virtualKey = VirtualKey.F9; return true;
            case Key.F10: virtualKey = VirtualKey.F10; return true;
            case Key.F11: virtualKey = VirtualKey.F11; return true;
            case Key.F12: virtualKey = VirtualKey.F12; return true;
            default: virtualKey = default; return false;
        }
    }
}
