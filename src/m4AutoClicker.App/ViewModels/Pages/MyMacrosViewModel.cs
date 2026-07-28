using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Domain.Hotkeys;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.App.ViewModels.Pages;

public sealed partial class MyMacrosViewModel : ObservableObject
{
    private readonly IMacroRepository _macroRepository;
    private readonly IMacroPlayer _macroPlayer;
    private readonly HotkeyCoordinatorService _hotkeyCoordinator;
    private readonly IApplicationLogger _logger;

    public MyMacrosViewModel(
        IMacroRepository macroRepository, IMacroPlayer macroPlayer, HotkeyCoordinatorService hotkeyCoordinator, IApplicationLogger logger)
    {
        _macroRepository = macroRepository;
        _macroPlayer = macroPlayer;
        _hotkeyCoordinator = hotkeyCoordinator;
        _logger = logger;

        // F8 artık burada seçilen makroyu oynatır (Aşama 5/6'daki "son kayıt" geçici çözümünün yerini alır).
        _hotkeyCoordinator.MacroPlaybackSourceProvider = LoadSelectedMacroAsync;
        _hotkeyCoordinator.MacroPlaybackToggled += OnMacroPlaybackToggled;
        _hotkeyCoordinator.HotkeyRegistrationsChanged += OnHotkeyRegistrationsChanged;
        // IMacroPlayer tek bir paylaşılan singleton olduğu için, duraklatma Makro Kaydedici
        // ekranından tetiklense bile bu ekran da durumu buradan öğrenir (aksi hâlde "Devam Et"
        // burada kalıcı olarak devre dışı kalabilirdi).
        _macroPlayer.PlaybackPausedChanged += OnPlaybackPausedChanged;

        HotkeyStatusText = DescribeHotkeyStatus(HotkeyIds.MacroPlaybackToggle);
    }

    public ObservableCollection<MacroSummary> Macros { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "Boşta.";

    [ObservableProperty]
    private string _hotkeyStatusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlaySelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveMacroDetailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
    private MacroSummary? _selectedMacro;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private string _editTagsText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlaySelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(PausePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopPlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveMacroDetailsCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PausePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumePlaybackCommand))]
    private bool _isPlaybackPaused;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var summaries = await _macroRepository.GetAllAsync();

            var selectedId = SelectedMacro?.Id;
            Macros.Clear();
            foreach (var summary in summaries)
            {
                Macros.Add(summary);
            }

            SelectedMacro = selectedId is null ? null : Macros.FirstOrDefault(m => m.Id == selectedId);
            StatusMessage = Macros.Count == 0 ? "Kayıtlı makro yok." : $"{Macros.Count} makro bulundu.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makro kütüphanesi yüklenemedi.");
            StatusMessage = $"Makrolar yüklenemedi: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlaySelected))]
    private Task PlaySelectedAsync() => _hotkeyCoordinator.ToggleMacroPlaybackAsync();

    private bool CanPlaySelected() => !IsPlaying && SelectedMacro is not null;

    [RelayCommand(CanExecute = nameof(CanPausePlayback))]
    private async Task PausePlaybackAsync()
    {
        await _macroPlayer.PauseAsync();
        StatusMessage = "Oynatma duraklatıldı.";
    }

    private bool CanPausePlayback() => IsPlaying && !IsPlaybackPaused;

    [RelayCommand(CanExecute = nameof(CanResumePlayback))]
    private async Task ResumePlaybackAsync()
    {
        await _macroPlayer.ResumeAsync();
        StatusMessage = "Oynatılıyor...";
    }

    private bool CanResumePlayback() => IsPlaying && IsPlaybackPaused;

    [RelayCommand(CanExecute = nameof(CanStopPlayback))]
    private async Task StopPlaybackAsync()
    {
        await _macroPlayer.StopAsync();
    }

    private bool CanStopPlayback() => IsPlaying;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var selected = SelectedMacro;
        if (selected is null)
        {
            return;
        }

        var confirmed = MessageBox.Show(
            $"'{selected.Name}' adlı makroyu silmek istediğinize emin misiniz? Bu işlem geri alınamaz.",
            "Makroyu Sil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (!confirmed)
        {
            return;
        }

        await _macroRepository.DeleteAsync(selected.Id);
        SelectedMacro = null;
        await RefreshAsync();
    }

    // Oynatma sırasında seçili makronun dosyasını silmek/dışa aktarmak/üzerine yazmak, o anda
    // oynatılmakta olan makronun altını oyabilir; bu yüzden oynatma bittikten sonraya bırakılır.
    private bool CanDeleteSelected() => SelectedMacro is not null && !IsPlaying;

    [RelayCommand(CanExecute = nameof(CanExportSelected))]
    private async Task ExportSelectedAsync()
    {
        var selected = SelectedMacro;
        if (selected is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Makroyu Dışa Aktar",
            FileName = $"{selected.Name}.json",
            Filter = "Makro dosyası (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _macroRepository.ExportAsync(selected.Id, dialog.FileName);
            StatusMessage = $"Makro dışa aktarıldı: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makro dışa aktarılamadı.");
            StatusMessage = $"Makro dışa aktarılamadı: {ex.Message}";
        }
    }

    private bool CanExportSelected() => SelectedMacro is not null && !IsPlaying;

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Makro İçe Aktar",
            Filter = "Makro dosyası (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var imported = await _macroRepository.ImportAsync(dialog.FileName);
            StatusMessage = $"Makro içe aktarıldı: {imported.Name}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Makro içe aktarılamadı.");
            StatusMessage = $"Makro içe aktarılamadı: {ex.Message}";
        }
    }

    partial void OnSelectedMacroChanged(MacroSummary? value)
    {
        EditDescription = value?.Description ?? string.Empty;
        EditTagsText = value is null ? string.Empty : string.Join(", ", value.Tags);
    }

    [RelayCommand(CanExecute = nameof(CanSaveMacroDetails))]
    private async Task SaveMacroDetailsAsync()
    {
        var selected = SelectedMacro;
        if (selected is null)
        {
            return;
        }

        var macro = await _macroRepository.GetByIdAsync(selected.Id);
        if (macro is null)
        {
            StatusMessage = "Makro artık mevcut değil.";
            return;
        }

        var tags = EditTagsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var updated = macro with
        {
            Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim(),
            Tags = tags,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _macroRepository.SaveAsync(updated);
        StatusMessage = "Makro bilgileri güncellendi.";
        await RefreshAsync();
    }

    private bool CanSaveMacroDetails() => SelectedMacro is not null && !IsPlaying;

    private async Task<Macro?> LoadSelectedMacroAsync()
    {
        var selected = SelectedMacro;
        return selected is null ? null : await _macroRepository.GetByIdAsync(selected.Id);
    }

    private void OnMacroPlaybackToggled(object? sender, MacroPlaybackHotkeyResultEventArgs e)
    {
        RunOnUiThread(() =>
        {
            IsPlaying = e.IsPlaying;
            IsPlaybackPaused = false;
            StatusMessage = e.StatusMessage;
        });
    }

    private void OnPlaybackPausedChanged(object? sender, bool isPaused)
    {
        RunOnUiThread(() => IsPlaybackPaused = isPaused);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private void OnHotkeyRegistrationsChanged(object? sender, EventArgs e)
    {
        RunOnUiThread(() => HotkeyStatusText = DescribeHotkeyStatus(HotkeyIds.MacroPlaybackToggle));
    }

    private string DescribeHotkeyStatus(string hotkeyId)
    {
        var definition = _hotkeyCoordinator.GetCurrentDefinition(hotkeyId);
        var label = definition is null ? "?" : HotkeyLabelFormatter.Format(definition);
        var result = _hotkeyCoordinator.GetRegistrationResult(hotkeyId);

        if (result is null)
        {
            return $"{label}: Bilinmiyor";
        }

        if (result.Success)
        {
            return $"{label}: Hazır";
        }

        return result.ErrorType == HotkeyRegistrationErrorType.RegistrationRejected
            ? $"{label}: Başka bir uygulama tarafından kullanılıyor"
            : $"{label}: Kaydedilemedi";
    }
}
