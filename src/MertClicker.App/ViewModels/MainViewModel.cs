using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MertClicker.App.ViewModels.Pages;

namespace MertClicker.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object _currentViewModel;

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    public MainViewModel(
        HomeViewModel home,
        AutoClickerViewModel autoClicker,
        MacroRecorderViewModel macroRecorder,
        MyMacrosViewModel myMacros,
        SettingsViewModel settings,
        LogsViewModel logs,
        AboutViewModel about)
    {
        NavigationItems =
        [
            new NavigationItem("Ana Sayfa", home),
            new NavigationItem("Auto Clicker", autoClicker),
            new NavigationItem("Makro Kaydedici", macroRecorder),
            new NavigationItem("Makrolarım", myMacros),
            new NavigationItem("Ayarlar", settings),
            new NavigationItem("Kayıtlar", logs),
            new NavigationItem("Hakkında", about)
        ];

        _currentViewModel = home;
    }

    [RelayCommand]
    private void Navigate(NavigationItem item)
    {
        CurrentViewModel = item.ViewModel;
    }
}
