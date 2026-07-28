using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Hotkeys;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.App.ViewModels.Pages;

public sealed partial class MacroRecorderViewModel : ObservableObject
{
    private readonly MacroRecorder _macroRecorder;
    private readonly IMacroPlayer _macroPlayer;
    private readonly IMacroRepository _macroRepository;
    private readonly MacroValidator _macroValidator;
    private readonly HotkeyCoordinatorService _hotkeyCoordinator;
    private readonly IHighResolutionClock _clock;

    public MacroRecorderViewModel(
        MacroRecorder macroRecorder,
        IMacroPlayer macroPlayer,
        IMacroRepository macroRepository,
        MacroValidator macroValidator,
        HotkeyCoordinatorService hotkeyCoordinator,
        IHighResolutionClock clock)
    {
        _macroRecorder = macroRecorder;
        _macroPlayer = macroPlayer;
        _macroRepository = macroRepository;
        _macroValidator = macroValidator;
        _hotkeyCoordinator = hotkeyCoordinator;
        _clock = clock;

        _hotkeyCoordinator.MacroRecorderToggled += OnMacroRecorderToggled;
        _hotkeyCoordinator.MacroPlaybackToggled += OnMacroPlaybackToggled;
        _hotkeyCoordinator.HotkeyRegistrationsChanged += OnHotkeyRegistrationsChanged;
        // IMacroPlayer tek bir paylaşılan singleton olduğu için, duraklatma Makrolarım ekranından
        // tetiklense bile bu ekran da durumu buradan öğrenir (aksi hâlde "Devam Et" burada kalıcı
        // olarak devre dışı kalabilirdi).
        _macroPlayer.PlaybackPausedChanged += OnPlaybackPausedChanged;
        // F8 ile hangi makronun oynatılacağı artık Makrolarım ekranındaki seçim tarafından belirlenir
        // (bkz. MyMacrosViewModel). "Son Kaydı Oynat" butonu burada IMacroPlayer'ı doğrudan kullanmaya
        // devam eder; bağımsız bir hızlı test imkânıdır.

        HotkeyStatusText = BuildHotkeyStatusText();
    }

    [ObservableProperty]
    private string _statusMessage = "Boşta.";

    [ObservableProperty]
    private string _hotkeyStatusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayLastRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveLastRecordingCommand))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayLastRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopPlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(PausePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveLastRecordingCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PausePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumePlaybackCommand))]
    private bool _isPlaybackPaused;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayLastRecordingCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveLastRecordingCommand))]
    private Macro? _lastRecordedMacro;

    [ObservableProperty]
    private string _lastRecordedMacroName = string.Empty;

    partial void OnLastRecordedMacroChanged(Macro? value) => LastRecordedMacroName = value?.Name ?? string.Empty;

    [RelayCommand(CanExecute = nameof(CanStartRecording))]
    private async Task StartRecordingAsync()
    {
        IsRecording = true;
        StatusMessage = "Kaydediliyor...";

        try
        {
            await _macroRecorder.StartAsync(CancellationToken.None, _hotkeyCoordinator.GetOtherHotkeyKeyCodesForRecording());
        }
        catch (Exception ex)
        {
            IsRecording = false;
            StatusMessage = $"Hata: {ex.Message}";
        }
    }

    private bool CanStartRecording() => !IsRecording && !IsPlaying;

    [RelayCommand(CanExecute = nameof(CanStopRecording))]
    private async Task StopRecordingAsync()
    {
        try
        {
            var macro = await _macroRecorder.StopAsync();
            LastRecordedMacro = macro;
            StatusMessage = BuildCompletionMessage(macro);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsRecording = false;
        }
    }

    private bool CanStopRecording() => IsRecording;

    [RelayCommand(CanExecute = nameof(CanPlayLastRecording))]
    private async Task PlayLastRecordingAsync()
    {
        var macro = LastRecordedMacro;
        if (macro is null)
        {
            return;
        }

        IsPlaying = true;
        IsPlaybackPaused = false;
        StatusMessage = "Oynatılıyor...";

        var options = new PlaybackOptions { SpeedMultiplier = 1.0, RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };
        var result = await _macroPlayer.PlayAsync(macro, options, CancellationToken.None);

        IsPlaying = false;
        IsPlaybackPaused = false;
        StatusMessage = result.Success
            ? $"Oynatma tamamlandı. {result.ExecutedActionCount} eylem çalıştırıldı."
            : $"Hata: {result.ErrorMessage}";

        if (result.DisplayMismatchWarning is not null)
        {
            StatusMessage = $"Uyarı: Ekran yapılandırması kayıttan farklı ({result.DisplayMismatchWarning}) {StatusMessage}";
        }
    }

    private bool CanPlayLastRecording() => !IsPlaying && !IsRecording && LastRecordedMacro is not null;

    [RelayCommand(CanExecute = nameof(CanStopPlayback))]
    private Task StopPlaybackAsync() => _macroPlayer.StopAsync();

    private bool CanStopPlayback() => IsPlaying;

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

    [RelayCommand(CanExecute = nameof(CanSaveLastRecording))]
    private async Task SaveLastRecordingAsync()
    {
        var macro = LastRecordedMacro;
        if (macro is null)
        {
            return;
        }

        var name = LastRecordedMacroName.Trim();
        var namedMacro = string.IsNullOrEmpty(name) || name == macro.Name ? macro : macro with { Name = name };

        var validation = _macroValidator.Validate(namedMacro);
        if (!validation.IsValid)
        {
            StatusMessage = $"Kaydedilemedi: {string.Join(" ", validation.Errors)}";
            return;
        }

        try
        {
            await _macroRepository.SaveAsync(namedMacro);
            LastRecordedMacro = namedMacro;
            StatusMessage = $"Makro kaydedildi: {namedMacro.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Kaydedilemedi: {ex.Message}";
        }
    }

    private bool CanSaveLastRecording() => !IsRecording && !IsPlaying && LastRecordedMacro is not null;

    private void OnMacroRecorderToggled(object? sender, MacroRecorderHotkeyResultEventArgs e)
    {
        RunOnUiThread(() =>
        {
            IsRecording = e.IsRecording;
            StatusMessage = e.StatusMessage;

            if (e.RecordedMacro is not null)
            {
                LastRecordedMacro = e.RecordedMacro;
            }
        });
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
        RunOnUiThread(() => HotkeyStatusText = BuildHotkeyStatusText());
    }

    private string BuildHotkeyStatusText() =>
        $"{DescribeHotkeyStatus(HotkeyIds.MacroRecorderToggle)}   |   {DescribeHotkeyStatus(HotkeyIds.MacroPlaybackToggle)}";

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

    private string BuildCompletionMessage(Macro macro)
    {
        var seconds = macro.DurationTicks / (double)_clock.Frequency;
        return $"Kayıt tamamlandı. {macro.Actions.Count} eylem, {seconds:0.0} saniye.";
    }
}
