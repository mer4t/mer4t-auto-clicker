using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MertClicker.App.ViewModels.Pages;

public sealed partial class AboutViewModel : ObservableObject
{
    public string ApplicationName => "MertClicker";

    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public string Description => "Windows için otomasyon uygulaması: auto clicker ve fare makrosu kaydedici/oynatıcı.";
}
