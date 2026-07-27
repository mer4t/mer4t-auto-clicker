using CommunityToolkit.Mvvm.ComponentModel;

namespace MertClicker.App.ViewModels.Pages;

public sealed partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _applicationStatus = "Boşta";

    [ObservableProperty]
    private string _activeOperation = "Yok";

    [ObservableProperty]
    private string _lastUsedMacroName = "Henüz makro çalıştırılmadı";

    [ObservableProperty]
    private string _lastRunResult = "-";
}
