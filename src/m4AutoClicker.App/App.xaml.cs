using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using m4AutoClicker.App.DependencyInjection;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.DependencyInjection;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Infrastructure.DependencyInjection;
using m4AutoClicker.Platform.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace m4AutoClicker.App;

public partial class App : System.Windows.Application
{
    // İkinci bir kopya global kısayolları (RegisterHotKey) çakışacak şekilde kaydetmeye çalışır;
    // bu da "kısayolu değiştirsem de eskisi hâlâ çalışıyor" gibi kafa karıştırıcı davranışlara yol
    // açar (arka planda unutulmuş eski bir kopya hâlâ eski tuşu, yeni açılan kopya ise yeni tuşu
    // tutmaya çalışır). Bu yüzden aynı oturumda ikinci bir örneğin açılması tamamen engellenir.
    private const string SingleInstanceMutexName = "m4AutoClicker.SingleInstance";

    private IHost? _host;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var createdNew);
        _ownsSingleInstanceMutex = createdNew;

        if (!createdNew)
        {
            MessageBox.Show(
                "m4 Auto Clicker zaten çalışıyor. Sistem tepsisindeki simgeyi kontrol edin.",
                "m4 Auto Clicker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.Addm4AutoClickerApplication();
                services.Addm4AutoClickerPlatformWindows();
                services.Addm4AutoClickerInfrastructure();
                services.Addm4AutoClickerApp();
            })
            .Build();

        _host.Start();

        var logger = _host.Services.GetRequiredService<IApplicationLogger>();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        logger.LogInformation(
            "m4 Auto Clicker başlatıldı. Sürüm: {0}, İşletim sistemi: {1}",
            version,
            Environment.OSVersion.VersionString);

        // Kaydedilmiş ayarlar, MacroOptimizer gibi tüketicilerin diski tekrar okumasına gerek
        // kalmadan senkron erişebilmesi için bellek içi sağlayıcıya (IApplicationSettingsProvider)
        // ana pencere oluşturulmadan önce yüklenir.
        var settingsRepository = _host.Services.GetRequiredService<ISettingsRepository>();
        var settingsProvider = _host.Services.GetRequiredService<IApplicationSettingsProvider>();
        // Task.Run ile çalıştırılır: WPF Dispatcher bu noktada henüz mesaj pompalamadığından,
        // doğrudan .GetAwaiter().GetResult() çağrısı LoadAsync içindeki await'in UI thread'ine
        // geri dönmeye çalışmasıyla kilitlenmeye (deadlock) yol açabilir.
        var settings = Task.Run(() => settingsRepository.LoadAsync()).GetAwaiter().GetResult();
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
            trayIconService.ShowBalloonTip("m4 Auto Clicker", "Acil durdurma uygulandı. Aktif otomasyonlar durduruldu.");
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
            logger.LogInformation("m4 Auto Clicker kapanıyor.");

            var emergencyStop = _host.Services.GetRequiredService<IEmergencyStopService>();
            // Aynı deadlock riski kapanışta da geçerli: Dispatcher artık mesaj pompalamadığından
            // Task.Run ile thread pool'a devredilir.
            Task.Run(() => emergencyStop.StopAllAsync()).GetAwaiter().GetResult();

            var hotkeyService = _host.Services.GetRequiredService<IHotkeyService>();
            hotkeyService.Dispose();

            var trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
            trayIconService.Dispose();

            Task.Run(() => _host.StopAsync()).GetAwaiter().GetResult();
            _host.Dispose();
        }

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();

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
            "m4 Auto Clicker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
