using System.Windows;
using m4AutoClicker.App.ViewModels;
using m4AutoClicker.Application.Abstractions;

namespace m4AutoClicker.App;

public partial class MainWindow : Window
{
    private readonly ITrayIconService _trayIconService;
    private bool _hasShownTrayHint;

    public MainWindow(MainViewModel viewModel, ITrayIconService trayIconService)
    {
        InitializeComponent();
        DataContext = viewModel;

        _trayIconService = trayIconService;
        _trayIconService.OpenRequested += OnTrayOpenRequested;

        StateChanged += OnStateChanged;
    }

    // Küçültme, pencereyi görev çubuğundan da kaldırıp sistem tepsisinde çalışmaya devam eder.
    // Kapatma (X) her zaman uygulamayı normal şekilde sonlandırır; tepsiden çıkış için ayrı menü var.
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        Hide();

        if (!_hasShownTrayHint)
        {
            _hasShownTrayHint = true;
            _trayIconService.ShowBalloonTip("m4 Auto Clicker", "Uygulama sistem tepsisinde çalışmaya devam ediyor.");
        }
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
