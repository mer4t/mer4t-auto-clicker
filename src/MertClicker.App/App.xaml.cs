using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using MertClicker.App.DependencyInjection;
using MertClicker.Application.Abstractions;
using MertClicker.Application.DependencyInjection;
using MertClicker.Application.Services;
using MertClicker.Infrastructure.DependencyInjection;
using MertClicker.Platform.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MertClicker.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddMertClickerApplication();
                services.AddMertClickerPlatformWindows();
                services.AddMertClickerInfrastructure();
                services.AddMertClickerApp();
            })
            .Build();

        _host.Start();

        var logger = _host.Services.GetRequiredService<IApplicationLogger>();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        logger.LogInformation(
            "MertClicker başlatıldı. Sürüm: {0}, İşletim sistemi: {1}",
            version,
            Environment.OSVersion.VersionString);

        // Kaydedilmiş ayarlar, MacroOptimizer gibi tüketicilerin diski tekrar okumasına gerek
        // kalmadan senkron erişebilmesi için bellek içi sağlayıcıya (IApplicationSettingsProvider)
        // ana pencere oluşturulmadan önce yüklenir.
        var settingsRepository = _host.Services.GetRequiredService<ISettingsRepository>();
        var settingsProvider = _host.Services.GetRequiredService<IApplicationSettingsProvider>();
        var settings = settingsRepository.LoadAsync().GetAwaiter().GetResult();
        settingsProvider.Update(settings);

        // Global hotkey mesaj alıcısı ve varsayılan kısayollar, ana pencere oluşturulup gösterilmeden
        // önce hazırlanır. AutoClickerViewModel (MainWindow -> MainViewModel zinciri üzerinden) kayıt
        // sonuçlarını okuyarak durum metnini bu noktadan sonra doğru şekilde oluşturabilir.
        var hotkeyService = _host.Services.GetRequiredService<IHotkeyService>();
        hotkeyService.Start();

        var hotkeyCoordinator = _host.Services.GetRequiredService<HotkeyCoordinatorService>();
        var registrationResults = hotkeyCoordinator.RegisterHotkeys(settings.Hotkeys);
        foreach (var result in registrationResults.Values)
        {
            if (!result.Success)
            {
                logger.LogWarning(
                    "Başlangıçta hotkey kaydı başarısız oldu: {0} - {1}", result.HotkeyId, result.ErrorMessage);
            }
        }

        // Tepsi simgesi, ana pencere gösterilmeden önce hazırlanır ki MainWindow constructor'ı
        // OpenRequested'e sorunsuz abone olabilsin.
        var trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
        trayIconService.Show();
        trayIconService.ExitRequested += OnTrayExitRequested;
        trayIconService.EmergencyStopRequested += OnTrayEmergencyStopRequested;

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        _host?.Services.GetRequiredService<IApplicationLogger>().LogInformation("Çıkış sistem tepsisinden istendi.");
        Shutdown();
    }

    private async void OnTrayEmergencyStopRequested(object? sender, EventArgs e)
    {
        if (_host is null)
        {
            return;
        }

        var logger = _host.Services.GetRequiredService<IApplicationLogger>();
        var trayIconService = _host.Services.GetRequiredService<ITrayIconService>();

        try
        {
            logger.LogInformation("Acil durdurma sistem tepsisinden tetiklendi.");
            var emergencyStop = _host.Services.GetRequiredService<IEmergencyStopService>();
            await emergencyStop.StopAllAsync();
            trayIconService.ShowBalloonTip("MertClicker", "Acil durdurma uygulandı. Aktif otomasyonlar durduruldu.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sistem tepsisinden acil durdurma sırasında hata oluştu.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var logger = _host.Services.GetRequiredService<IApplicationLogger>();
            logger.LogInformation("MertClicker kapanıyor.");

            var emergencyStop = _host.Services.GetRequiredService<IEmergencyStopService>();
            emergencyStop.StopAllAsync().GetAwaiter().GetResult();

            var hotkeyService = _host.Services.GetRequiredService<IHotkeyService>();
            hotkeyService.Dispose();

            var trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
            trayIconService.Dispose();

            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleUnhandledException(e.Exception);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            HandleUnhandledException(exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleUnhandledException(e.Exception);
        e.SetObserved();
    }

    private void HandleUnhandledException(Exception exception)
    {
        _host?.Services.GetService<IApplicationLogger>()?.LogError(exception, "Yakalanmamış hata oluştu.");

        MessageBox.Show(
            "Beklenmeyen bir hata oluştu. Ayrıntılar log dosyasına kaydedildi.",
            "MertClicker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
