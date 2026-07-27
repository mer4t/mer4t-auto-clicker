using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using MertClicker.Application.Services;
using MertClicker.Domain;
using MertClicker.Domain.Automation;
using MertClicker.Domain.Display;
using MertClicker.Domain.Hotkeys;

namespace MertClicker.App.ViewModels.Pages;

public enum IntervalUnit
{
    Milliseconds,
    Seconds
}

public sealed partial class AutoClickerViewModel : ObservableObject
{
    private readonly AutoClickerService _autoClickerService;
    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly HotkeyCoordinatorService _hotkeyCoordinator;

    public AutoClickerViewModel(
        AutoClickerService autoClickerService,
        ICursorPositionProvider cursorPositionProvider,
        HotkeyCoordinatorService hotkeyCoordinator)
    {
        _autoClickerService = autoClickerService;
        _cursorPositionProvider = cursorPositionProvider;
        _hotkeyCoordinator = hotkeyCoordinator;

        _hotkeyCoordinator.AutoClickerPlanProvider = BuildClickPlan;
        _hotkeyCoordinator.AutoClickerToggled += OnAutoClickerToggled;
        _hotkeyCoordinator.NotificationRaised += OnHotkeyNotificationRaised;
        _hotkeyCoordinator.HotkeyRegistrationsChanged += OnHotkeyRegistrationsChanged;

        HotkeyStatusText = BuildHotkeyStatusText();
    }

    public IReadOnlyList<MouseButton> MouseButtonOptions { get; } = Enum.GetValues<MouseButton>();

    public IReadOnlyList<ClickType> ClickTypeOptions { get; } = Enum.GetValues<ClickType>();

    public IReadOnlyList<RepeatMode> RepeatModeOptions { get; } = Enum.GetValues<RepeatMode>();

    public IReadOnlyList<IntervalUnit> IntervalUnitOptions { get; } = Enum.GetValues<IntervalUnit>();

    [ObservableProperty]
    private MouseButton _selectedButton = MouseButton.Left;

    [ObservableProperty]
    private ClickType _selectedClickType = ClickType.Single;

    [ObservableProperty]
    private double _intervalValue = 100;

    [ObservableProperty]
    private IntervalUnit _selectedIntervalUnit = IntervalUnit.Milliseconds;

    [ObservableProperty]
    private RepeatMode _selectedRepeatMode = RepeatMode.UntilStopped;

    [ObservableProperty]
    private int _repeatCount = 10;

    [ObservableProperty]
    private bool _useFixedPoint;

    [ObservableProperty]
    private int _fixedX;

    [ObservableProperty]
    private int _fixedY;

    [ObservableProperty]
    private double _startDelaySeconds;

    [ObservableProperty]
    private string _statusMessage = "Boşta.";

    [ObservableProperty]
    private string? _captureCountdownText;

    [ObservableProperty]
    private string _hotkeyStatusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    private bool _isPaused;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsRunning = true;
        IsPaused = false;
        StatusMessage = "Çalışıyor...";

        var plan = BuildClickPlan();
        var result = await _autoClickerService.StartAsync(plan, CancellationToken.None);

        IsRunning = false;
        IsPaused = false;
        StatusMessage = result.Success
            ? $"Tamamlandı. {result.ExecutedActionCount} eylem çalıştırıldı."
            : $"Hata: {result.ErrorMessage}";
    }

    private bool CanStart() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => _autoClickerService.StopAsync();

    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        await _autoClickerService.PauseAsync();
        IsPaused = true;
        StatusMessage = "Duraklatıldı.";
    }

    private bool CanPause() => IsRunning && !IsPaused;

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        await _autoClickerService.ResumeAsync();
        IsPaused = false;
        StatusMessage = "Çalışıyor...";
    }

    private bool CanResume() => IsRunning && IsPaused;

    [RelayCommand]
    private async Task CaptureCoordinateAsync()
    {
        for (var remaining = 3; remaining > 0; remaining--)
        {
            CaptureCountdownText = $"{remaining} saniye içinde imleç konumu yakalanacak...";
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var position = _cursorPositionProvider.GetCurrentPosition();
        FixedX = position.X;
        FixedY = position.Y;
        CaptureCountdownText = $"Yakalanan konum: ({FixedX}, {FixedY})";
    }

    private void OnAutoClickerToggled(object? sender, AutoClickerHotkeyResultEventArgs e)
    {
        RunOnUiThread(() =>
        {
            IsRunning = e.IsRunning;
            IsPaused = false;
            StatusMessage = e.StatusMessage;
        });
    }

    private void OnHotkeyNotificationRaised(object? sender, string message)
    {
        RunOnUiThread(() => StatusMessage = message);
    }

    private void OnHotkeyRegistrationsChanged(object? sender, EventArgs e)
    {
        RunOnUiThread(() => HotkeyStatusText = BuildHotkeyStatusText());
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

    private string BuildHotkeyStatusText()
    {
        var autoClicker = DescribeHotkeyStatus(HotkeyIds.AutoClickerToggle);
        var emergencyStop = DescribeHotkeyStatus(HotkeyIds.EmergencyStop);
        return $"{autoClicker}   |   {emergencyStop}";
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

    private ClickPlan BuildClickPlan()
    {
        var interval = SelectedIntervalUnit == IntervalUnit.Seconds
            ? TimeSpan.FromSeconds(IntervalValue)
            : TimeSpan.FromMilliseconds(IntervalValue);

        return new ClickPlan
        {
            Button = SelectedButton,
            ClickType = SelectedClickType,
            Interval = interval,
            RepeatMode = SelectedRepeatMode,
            RepeatCount = SelectedRepeatMode == RepeatMode.FixedCount ? RepeatCount : null,
            Target = UseFixedPoint
                ? CoordinateTarget.FixedPoint(new ScreenPoint(FixedX, FixedY))
                : CoordinateTarget.CurrentCursor,
            StartDelay = TimeSpan.FromSeconds(StartDelaySeconds)
        };
    }
}
