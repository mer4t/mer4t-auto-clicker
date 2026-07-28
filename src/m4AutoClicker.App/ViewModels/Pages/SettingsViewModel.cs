using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.App.ViewModels.Pages;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IApplicationSettingsProvider _settingsProvider;
    private readonly HotkeyCoordinatorService _hotkeyCoordinator;
    private readonly IApplicationLogger _logger;

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IApplicationSettingsProvider settingsProvider,
        HotkeyCoordinatorService hotkeyCoordinator,
        IApplicationLogger logger)
    {
        _settingsRepository = settingsRepository;
        _settingsProvider = settingsProvider;
        _hotkeyCoordinator = hotkeyCoordinator;
        _logger = logger;

        HotkeyBindings =
        [
            new HotkeySettingViewModel { HotkeyId = HotkeyIds.AutoClickerToggle, Label = "Auto Clicker Başlat/Durdur" },
            new HotkeySettingViewModel { HotkeyId = HotkeyIds.MacroRecorderToggle, Label = "Makro Kaydı Başlat/Durdur" },
            new HotkeySettingViewModel { HotkeyId = HotkeyIds.MacroPlaybackToggle, Label = "Makro Oynat/Durdur" },
            new HotkeySettingViewModel { HotkeyId = HotkeyIds.EmergencyStop, Label = "Acil Durdur" }
        ];

        // Uygulama açılışında zaten yüklenmiş olan güncel değerlerle başlar; kullanıcı sayfaya
        // gelmeden önce disk tekrar okunmaz.
        ApplyToFields(_settingsProvider.Current);
        RefreshHotkeyStatusTexts();
    }

    public IReadOnlyList<HotkeySettingViewModel> HotkeyBindings { get; }

    [ObservableProperty]
    private int _minimumIntervalMilliseconds;

    [ObservableProperty]
    private int _minimumDistancePixels;

    [ObservableProperty]
    private string _statusMessage = "Boşta.";

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var settings = await _settingsRepository.LoadAsync();
            ApplyToFields(settings);
            _settingsProvider.Update(settings);
            StatusMessage = "Ayarlar yüklendi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar yüklenemedi.");
            StatusMessage = $"Ayarlar yüklenemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (MinimumIntervalMilliseconds < 0 || MinimumDistancePixels < 0)
        {
            StatusMessage = "Değerler negatif olamaz.";
            return;
        }

        var hotkeyDefinitions = HotkeyBindings.Select(b => b.ToDefinition()).ToList();

        var settings = new ApplicationSettings
        {
            MouseMovementSampling = new MouseMovementSamplingSettings
            {
                MinimumIntervalMilliseconds = MinimumIntervalMilliseconds,
                MinimumDistancePixels = MinimumDistancePixels
            },
            Hotkeys = hotkeyDefinitions
        };

        try
        {
            await _settingsRepository.SaveAsync(settings);
            // Diske yazma başarılı olduktan sonra bellekteki güncel değeri güncelle; böylece
            // MacroOptimizer gibi tüketiciler uygulama yeniden başlatılmadan yeni değerleri kullanır.
            _settingsProvider.Update(settings);

            // Değişen kısayolları uygulama yeniden başlatılmadan canlı olarak yeniden kaydeder.
            var results = _hotkeyCoordinator.ReassignHotkeys(hotkeyDefinitions);
            RefreshHotkeyStatusTexts(results);

            // Bir kombinasyon çakışma nedeniyle reddedilmişse koordinatör önceki çalışan
            // kombinasyonu geri yükler; arayüzün her zaman gerçekten etkin olan kombinasyonu
            // göstermesi için satırları koordinatördeki güncel tanımlarla senkronize et.
            foreach (var binding in HotkeyBindings)
            {
                var current = _hotkeyCoordinator.GetCurrentDefinition(binding.HotkeyId);
                if (current is not null)
                {
                    binding.ApplyFrom(current);
                }
            }

            var failedCount = results.Values.Count(r => !r.Success);
            StatusMessage = failedCount == 0
                ? "Ayarlar kaydedildi."
                : $"Ayarlar kaydedildi, ancak {failedCount} kısayol kaydedilemedi (aşağıdaki durumlara bakın).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar kaydedilemedi.");
            StatusMessage = $"Ayarlar kaydedilemedi: {ex.Message}";
        }
    }

    private void ApplyToFields(ApplicationSettings settings)
    {
        MinimumIntervalMilliseconds = settings.MouseMovementSampling.MinimumIntervalMilliseconds;
        MinimumDistancePixels = settings.MouseMovementSampling.MinimumDistancePixels;

        foreach (var binding in HotkeyBindings)
        {
            var definition = settings.Hotkeys.FirstOrDefault(h => h.Id == binding.HotkeyId)
                ?? HotkeyDefaults.All.First(h => h.Id == binding.HotkeyId);
            binding.ApplyFrom(definition);
        }
    }

    private void RefreshHotkeyStatusTexts(IReadOnlyDictionary<string, HotkeyRegistrationResult>? results = null)
    {
        foreach (var binding in HotkeyBindings)
        {
            var result = results is not null
                ? results.GetValueOrDefault(binding.HotkeyId)
                : _hotkeyCoordinator.GetRegistrationResult(binding.HotkeyId);

            binding.StatusText = DescribeResult(result);
        }
    }

    private static string DescribeResult(HotkeyRegistrationResult? result)
    {
        if (result is null)
        {
            return "Bilinmiyor";
        }

        if (result.Success)
        {
            return "Hazır";
        }

        return result.ErrorType switch
        {
            HotkeyRegistrationErrorType.RegistrationRejected => "Başka bir uygulama tarafından kullanılıyor",
            HotkeyRegistrationErrorType.CombinationAlreadyUsed => "Bu kombinasyon başka bir m4AutoClicker kısayolu tarafından kullanılıyor",
            _ => "Kaydedilemedi"
        };
    }
}
