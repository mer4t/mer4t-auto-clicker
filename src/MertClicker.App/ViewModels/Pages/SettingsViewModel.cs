using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using MertClicker.Application.Services;
using MertClicker.Domain.Hotkeys;

namespace MertClicker.App.ViewModels.Pages;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IApplicationSettingsProvider _settingsProvider;
    private readonly HotkeyCoordinatorService _hotkeyCoordinator;

    public SettingsViewModel(
        ISettingsRepository settingsRepository, IApplicationSettingsProvider settingsProvider, HotkeyCoordinatorService hotkeyCoordinator)
    {
        _settingsRepository = settingsRepository;
        _settingsProvider = settingsProvider;
        _hotkeyCoordinator = hotkeyCoordinator;

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

            var failedCount = results.Values.Count(r => !r.Success);
            StatusMessage = failedCount == 0
                ? "Ayarlar kaydedildi."
                : $"Ayarlar kaydedildi, ancak {failedCount} kısayol kaydedilemedi (aşağıdaki durumlara bakın).";
        }
        catch (Exception ex)
        {
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
            HotkeyRegistrationErrorType.CombinationAlreadyUsed => "Bu kombinasyon başka bir MertClicker kısayolu tarafından kullanılıyor",
            _ => "Kaydedilemedi"
        };
    }
}
