using MertClicker.Application.Models;
using MertClicker.Application.Services;
using MertClicker.Application.Tests.Fakes;
using MertClicker.Domain;
using MertClicker.Domain.Automation;
using MertClicker.Domain.Display;
using MertClicker.Domain.Hotkeys;
using MertClicker.Domain.Macros;

namespace MertClicker.Application.Tests.Services;

public class HotkeyCoordinatorServiceTests
{
    private static (
        HotkeyCoordinatorService Coordinator,
        AutoClickerService AutoClickerService,
        MacroRecorder MacroRecorder,
        MacroPlayer MacroPlayer,
        FakeInputCaptureProvider CaptureProvider,
        FakeInputInjector Injector,
        FakeHotkeyService HotkeyService,
        FakeEmergencyStopService EmergencyStopService,
        FakeApplicationLogger Logger) CreateCoordinator()
    {
        var injector = new FakeInputInjector();
        var clock = new RealElapsedClock();
        var scheduler = new PlaybackScheduler(clock);
        var logger = new FakeApplicationLogger();
        var engine = new AutomationEngine(injector, clock, scheduler, logger);
        var coordinateResolver = new CoordinateResolver();
        var displayService = new FakeDisplayService();
        var autoClickerService = new AutoClickerService(engine, coordinateResolver, displayService, clock);
        var macroPlayer = new MacroPlayer(engine, displayService, logger);

        var captureProvider = new FakeInputCaptureProvider();
        var keyboardCaptureProvider = new FakeKeyboardCaptureProvider();
        var optimizer = new MacroOptimizer(clock, new FakeApplicationSettingsProvider());
        var macroRecorder = new MacroRecorder(captureProvider, keyboardCaptureProvider, clock, displayService, optimizer, logger);

        var hotkeyService = new FakeHotkeyService();
        var emergencyStopService = new FakeEmergencyStopService();

        var coordinator = new HotkeyCoordinatorService(
            hotkeyService, autoClickerService, macroRecorder, macroPlayer, emergencyStopService, logger);

        return (coordinator, autoClickerService, macroRecorder, macroPlayer, captureProvider, injector, hotkeyService, emergencyStopService, logger);
    }

    private static ClickPlan FixedCountPlan(int repeatCount = 1) => new()
    {
        Button = MouseButton.Left,
        ClickType = ClickType.Single,
        Interval = TimeSpan.FromMilliseconds(5),
        RepeatMode = RepeatMode.FixedCount,
        RepeatCount = repeatCount,
        Target = CoordinateTarget.CurrentCursor
    };

    private static ClickPlan UntilStoppedPlan() => new()
    {
        Button = MouseButton.Left,
        ClickType = ClickType.Single,
        Interval = TimeSpan.FromMilliseconds(5),
        RepeatMode = RepeatMode.UntilStopped,
        Target = CoordinateTarget.CurrentCursor
    };

    private static Macro SingleClickMacro() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Makro",
        SchemaVersion = 1,
        CreatedAtUtc = DateTime.UnixEpoch,
        UpdatedAtUtc = DateTime.UnixEpoch,
        DurationTicks = 100,
        DisplaySnapshot = new DisplaySnapshot { VirtualLeft = 0, VirtualTop = 0, VirtualWidth = 1920, VirtualHeight = 1080, Monitors = [] },
        Actions =
        [
            new MouseMoveAction { OffsetTicks = 0, X = 10, Y = 10 },
            new MouseButtonDownAction { OffsetTicks = 10, Button = MouseButton.Left },
            new MouseButtonUpAction { OffsetTicks = 20, Button = MouseButton.Left }
        ]
    };

    [Fact]
    public void RegisterDefaultHotkeys_Registers_All_Four_With_Correct_Keys()
    {
        var (coordinator, _, _, _, _, _, hotkeyService, _, _) = CreateCoordinator();

        var results = coordinator.RegisterDefaultHotkeys();

        Assert.Equal(4, results.Count);
        Assert.True(results[HotkeyIds.AutoClickerToggle].Success);
        Assert.True(results[HotkeyIds.MacroRecorderToggle].Success);
        Assert.True(results[HotkeyIds.MacroPlaybackToggle].Success);
        Assert.True(results[HotkeyIds.EmergencyStop].Success);
        Assert.False(hotkeyService.Started); // Start(), coordinator tarafından değil App tarafından çağrılır.
    }

    [Fact]
    public void GetOtherHotkeyKeyCodesForRecording_Includes_The_Three_Non_Recorder_Hotkeys_But_Not_The_Recorder_Toggle()
    {
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();
        coordinator.RegisterDefaultHotkeys();

        var codes = coordinator.GetOtherHotkeyKeyCodesForRecording();

        Assert.Contains((ushort)VirtualKey.F6, codes); // AutoClickerToggle
        Assert.Contains((ushort)VirtualKey.F8, codes); // MacroPlaybackToggle
        Assert.Contains((ushort)VirtualKey.F9, codes); // EmergencyStop
        Assert.DoesNotContain((ushort)VirtualKey.F7, codes); // MacroRecorderToggle - hariç
    }

    [Fact]
    public void RegisterHotkeys_Raises_HotkeyRegistrationsChanged()
    {
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();
        var raisedCount = 0;
        coordinator.HotkeyRegistrationsChanged += (_, _) => raisedCount++;

        coordinator.RegisterDefaultHotkeys();

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void ReassignHotkeys_With_New_Combination_Succeeds()
    {
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();
        coordinator.RegisterDefaultHotkeys();

        var newBindings = new List<HotkeyDefinition>
        {
            new() { Id = HotkeyIds.AutoClickerToggle, Key = VirtualKey.F1, Modifiers = HotkeyModifiers.Control },
            new() { Id = HotkeyIds.MacroRecorderToggle, Key = VirtualKey.F7 },
            new() { Id = HotkeyIds.MacroPlaybackToggle, Key = VirtualKey.F8 },
            new() { Id = HotkeyIds.EmergencyStop, Key = VirtualKey.F9 }
        };

        var results = coordinator.ReassignHotkeys(newBindings);

        Assert.True(results[HotkeyIds.AutoClickerToggle].Success);
        Assert.True(results.Values.All(r => r.Success));
    }

    [Fact]
    public async Task ReassignHotkeys_Swapping_Two_Keys_Does_Not_Cause_Transient_Conflict()
    {
        // F6<->F7 takas edilirse, önce her ikisini de bırakıp SONRA yeniden kaydetmek gerekir;
        // aksi halde biri hâlâ diğerinin eski tuşunu tutarken kayıt denemesi CombinationAlreadyUsed
        // ile başarısız olurdu.
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();
        coordinator.RegisterDefaultHotkeys();

        var swapped = new List<HotkeyDefinition>
        {
            new() { Id = HotkeyIds.AutoClickerToggle, Key = VirtualKey.F7 },
            new() { Id = HotkeyIds.MacroRecorderToggle, Key = VirtualKey.F6 },
            new() { Id = HotkeyIds.MacroPlaybackToggle, Key = VirtualKey.F8 },
            new() { Id = HotkeyIds.EmergencyStop, Key = VirtualKey.F9 }
        };

        var results = coordinator.ReassignHotkeys(swapped);

        Assert.True(results[HotkeyIds.AutoClickerToggle].Success);
        Assert.True(results[HotkeyIds.MacroRecorderToggle].Success);

        // F7 artık AutoClicker'ı tetiklemeli, F6 değil.
        var autoClickerEvents = new List<AutoClickerHotkeyResultEventArgs>();
        coordinator.AutoClickerToggled += (_, e) => autoClickerEvents.Add(e);
        coordinator.AutoClickerPlanProvider = () => FixedCountPlan();

        await coordinator.ToggleAutoClickerAsync();

        Assert.NotEmpty(autoClickerEvents);
    }

    [Fact]
    public async Task ToggleAutoClickerAsync_Starts_When_Idle_Using_Plan_Provider()
    {
        var (coordinator, _, _, _, _, injector, _, _, _) = CreateCoordinator();
        coordinator.AutoClickerPlanProvider = () => FixedCountPlan(3);

        var events = new List<AutoClickerHotkeyResultEventArgs>();
        coordinator.AutoClickerToggled += (_, e) => events.Add(e);

        await coordinator.ToggleAutoClickerAsync();

        Assert.Equal(3, injector.PressedButtons.Count);
        Assert.Equal(2, events.Count);
        Assert.True(events[0].IsRunning);
        Assert.False(events[1].IsRunning);
        Assert.Contains("Tamamlandı", events[1].StatusMessage);
    }

    [Fact]
    public async Task ToggleAutoClickerAsync_Second_Call_While_Running_Stops_Instead_Of_Starting_Again()
    {
        var (coordinator, _, _, _, _, injector, _, _, _) = CreateCoordinator();
        coordinator.AutoClickerPlanProvider = () => UntilStoppedPlan();

        var firstToggle = coordinator.ToggleAutoClickerAsync();
        await Task.Delay(30);

        // İkinci çağrı, ilk otomasyon hâlâ çalışırken gelir; yeni bir otomasyon başlatmak yerine
        // çalışanı durdurmalı (gerçek F6 çift basış senaryosu).
        await coordinator.ToggleAutoClickerAsync();
        await firstToggle;

        Assert.True(injector.PressedButtons.Count > 0);
        Assert.Equal(injector.PressedButtons.Count, injector.ReleasedButtons.Count);
    }

    [Fact]
    public async Task ToggleAutoClickerAsync_Does_Not_Crash_When_Plan_Provider_Missing()
    {
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();

        var events = new List<AutoClickerHotkeyResultEventArgs>();
        coordinator.AutoClickerToggled += (_, e) => events.Add(e);

        await coordinator.ToggleAutoClickerAsync();

        Assert.Single(events);
        Assert.False(events[0].IsRunning);
    }

    [Fact]
    public async Task ToggleAutoClickerAsync_Does_Not_Crash_When_Plan_Provider_Throws()
    {
        var (coordinator, _, _, _, _, _, _, _, logger) = CreateCoordinator();
        coordinator.AutoClickerPlanProvider = () => throw new InvalidOperationException("bozuk ayar");

        var events = new List<AutoClickerHotkeyResultEventArgs>();
        coordinator.AutoClickerToggled += (_, e) => events.Add(e);

        var exception = await Record.ExceptionAsync(() => coordinator.ToggleAutoClickerAsync());

        Assert.Null(exception);
        Assert.Single(events);
        Assert.False(events[0].IsRunning);
        Assert.Contains(logger.ErrorMessages, m => m.Contains("tıklama planı"));
    }

    [Fact]
    public async Task ToggleAutoClickerAsync_Reports_Graceful_Failure_For_Invalid_Plan()
    {
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();
        coordinator.AutoClickerPlanProvider = () => FixedCountPlan(3) with { Interval = TimeSpan.Zero };

        var events = new List<AutoClickerHotkeyResultEventArgs>();
        coordinator.AutoClickerToggled += (_, e) => events.Add(e);

        await coordinator.ToggleAutoClickerAsync();

        Assert.Equal(2, events.Count);
        Assert.False(events[1].IsRunning);
        Assert.Contains("Hata", events[1].StatusMessage);
    }

    [Fact]
    public async Task ToggleMacroRecorderAsync_Starts_Recording_When_Idle()
    {
        var (coordinator, _, macroRecorder, _, _, _, _, _, _) = CreateCoordinator();

        var events = new List<MacroRecorderHotkeyResultEventArgs>();
        coordinator.MacroRecorderToggled += (_, e) => events.Add(e);

        await coordinator.ToggleMacroRecorderAsync();

        Assert.Equal(RecordingState.Recording, macroRecorder.State);
        Assert.Single(events);
        Assert.True(events[0].IsRecording);
    }

    [Fact]
    public async Task ToggleMacroRecorderAsync_Stops_Recording_And_Returns_Macro_When_Recording()
    {
        var (coordinator, _, macroRecorder, _, captureProvider, _, _, _, _) = CreateCoordinator();

        var events = new List<MacroRecorderHotkeyResultEventArgs>();
        coordinator.MacroRecorderToggled += (_, e) => events.Add(e);

        await coordinator.ToggleMacroRecorderAsync();
        await captureProvider.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonDown, X = 1, Y = 1, TimestampTicks = 0, IsInjectedByApplication = false
        });

        await coordinator.ToggleMacroRecorderAsync();

        Assert.Equal(RecordingState.Idle, macroRecorder.State);
        Assert.Equal(2, events.Count);
        Assert.False(events[1].IsRecording);
        Assert.NotNull(events[1].RecordedMacro);
        Assert.Single(events[1].RecordedMacro!.Actions);
        Assert.Contains("tamamlandı", events[1].StatusMessage);
    }

    [Fact]
    public async Task ToggleMacroRecorderAsync_Rapid_Double_Call_Does_Not_Throw()
    {
        var (coordinator, _, macroRecorder, _, _, _, _, _, _) = CreateCoordinator();

        var firstToggle = coordinator.ToggleMacroRecorderAsync();
        await firstToggle;

        var exception = await Record.ExceptionAsync(() => coordinator.ToggleMacroRecorderAsync());

        Assert.Null(exception);
        Assert.Equal(RecordingState.Idle, macroRecorder.State);
    }

    [Fact]
    public async Task ToggleMacroPlaybackAsync_Plays_Macro_From_Source_Provider()
    {
        var (coordinator, _, _, _, _, injector, _, _, _) = CreateCoordinator();
        coordinator.MacroPlaybackSourceProvider = () => Task.FromResult<Macro?>(SingleClickMacro());

        var events = new List<MacroPlaybackHotkeyResultEventArgs>();
        coordinator.MacroPlaybackToggled += (_, e) => events.Add(e);

        await coordinator.ToggleMacroPlaybackAsync();

        Assert.Single(injector.PressedButtons);
        Assert.Equal(2, events.Count);
        Assert.True(events[0].IsPlaying);
        Assert.False(events[1].IsPlaying);
        Assert.Contains("Tamamlandı", events[1].StatusMessage);
    }

    [Fact]
    public async Task ToggleMacroPlaybackAsync_Does_Not_Crash_When_No_Macro_Available()
    {
        var (coordinator, _, _, _, _, _, _, _, _) = CreateCoordinator();

        var events = new List<MacroPlaybackHotkeyResultEventArgs>();
        coordinator.MacroPlaybackToggled += (_, e) => events.Add(e);

        await coordinator.ToggleMacroPlaybackAsync();

        Assert.Single(events);
        Assert.False(events[0].IsPlaying);
    }

    [Fact]
    public async Task ToggleMacroPlaybackAsync_Second_Call_While_Playing_Stops_Playback()
    {
        var (coordinator, _, _, macroPlayer, _, injector, _, _, _) = CreateCoordinator();
        coordinator.MacroPlaybackSourceProvider = () => Task.FromResult<Macro?>(SingleClickMacro() with
        {
            Actions = [new DelayAction { OffsetTicks = 0, DurationTicks = System.Diagnostics.Stopwatch.Frequency * 5 }]
        });

        var firstToggle = coordinator.ToggleMacroPlaybackAsync();
        await Task.Delay(30);

        Assert.Equal(PlaybackState.Playing, macroPlayer.State);

        await coordinator.ToggleMacroPlaybackAsync();
        await firstToggle;

        Assert.Equal(PlaybackState.Idle, macroPlayer.State);
    }

    [Fact]
    public async Task EmergencyStopAsync_Calls_EmergencyStopService_And_Raises_Notification()
    {
        var (coordinator, _, _, _, _, _, _, emergencyStopService, _) = CreateCoordinator();

        var notifications = new List<string>();
        coordinator.NotificationRaised += (_, message) => notifications.Add(message);

        await coordinator.EmergencyStopAsync();

        Assert.Equal(1, emergencyStopService.CallCount);
        Assert.Contains(notifications, m => m.Contains("Acil durdurma"));
    }

    [Fact]
    public async Task EmergencyStopAsync_Is_Safe_When_Called_Twice_In_A_Row()
    {
        var (coordinator, _, _, _, _, _, _, emergencyStopService, _) = CreateCoordinator();

        await coordinator.EmergencyStopAsync();
        var exception = await Record.ExceptionAsync(() => coordinator.EmergencyStopAsync());

        Assert.Null(exception);
        Assert.Equal(2, emergencyStopService.CallCount);
    }

    [Fact]
    public void HotkeyPressed_With_Unknown_Id_Is_Logged_And_Does_Not_Crash()
    {
        var (coordinator, _, _, _, _, _, hotkeyService, _, logger) = CreateCoordinator();

        var exception = Record.Exception(() => hotkeyService.RaiseHotkeyPressed("Unknown.Hotkey"));

        Assert.Null(exception);
        Assert.Contains(logger.WarningMessages, m => m.Contains("Bilinmeyen"));
    }
}
