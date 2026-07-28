using m4AutoClicker.Application.Models;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Application.Tests.Fakes;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Tests.Services;

public class EmergencyStopCoordinatorTests
{
    private static (
        EmergencyStopCoordinator Coordinator,
        AutoClickerService AutoClickerService,
        MacroRecorder MacroRecorder,
        MacroPlayer MacroPlayer,
        FakeInputCaptureProvider CaptureProvider,
        FakeInputInjector Injector,
        FakeApplicationLogger Logger) Create()
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

        var coordinator = new EmergencyStopCoordinator(autoClickerService, macroRecorder, macroPlayer, logger);

        return (coordinator, autoClickerService, macroRecorder, macroPlayer, captureProvider, injector, logger);
    }

    [Fact]
    public async Task StopAllAsync_Stops_An_Active_UntilStopped_Automation()
    {
        var (coordinator, autoClickerService, _, _, _, injector, _) = Create();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(5),
            RepeatMode = RepeatMode.UntilStopped,
            Target = CoordinateTarget.CurrentCursor
        };

        var runTask = autoClickerService.StartAsync(plan, CancellationToken.None);
        await Task.Delay(30);

        await coordinator.StopAllAsync();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.True(injector.PressedButtons.Count > 0);
        Assert.Equal(injector.PressedButtons.Count, injector.ReleasedButtons.Count);
    }

    [Fact]
    public async Task StopAllAsync_Stops_An_Active_Macro_Recording()
    {
        var (coordinator, _, macroRecorder, _, captureProvider, _, logger) = Create();

        await macroRecorder.StartAsync(CancellationToken.None);
        await captureProvider.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonDown, X = 1, Y = 1, TimestampTicks = 0, IsInjectedByApplication = false
        });

        await coordinator.StopAllAsync();

        Assert.Equal(RecordingState.Idle, macroRecorder.State);
        Assert.Contains(logger.InformationMessages, m => m.Contains("makro kaydı durduruldu"));
    }

    [Fact]
    public async Task StopAllAsync_Stops_An_Active_Macro_Playback()
    {
        var (coordinator, _, _, macroPlayer, _, injector, _) = Create();
        var macro = new Macro
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            SchemaVersion = 1,
            CreatedAtUtc = DateTime.UnixEpoch,
            UpdatedAtUtc = DateTime.UnixEpoch,
            DurationTicks = 0,
            DisplaySnapshot = new DisplaySnapshot { VirtualLeft = 0, VirtualTop = 0, VirtualWidth = 1920, VirtualHeight = 1080, Monitors = [] },
            Actions = [new DelayAction { OffsetTicks = 0, DurationTicks = System.Diagnostics.Stopwatch.Frequency * 5 }]
        };
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var playTask = macroPlayer.PlayAsync(macro, options, CancellationToken.None);
        await Task.Delay(30);

        await coordinator.StopAllAsync();
        var result = await playTask;

        Assert.True(result.Success);
        Assert.Equal(PlaybackState.Idle, macroPlayer.State);
        Assert.Empty(injector.PressedButtons);
    }

    [Fact]
    public async Task StopAllAsync_Is_Safe_When_Nothing_Is_Running()
    {
        var (coordinator, _, _, _, _, _, _) = Create();

        var exception = await Record.ExceptionAsync(() => coordinator.StopAllAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task StopAllAsync_Is_Safe_When_Called_Twice_In_A_Row()
    {
        var (coordinator, _, _, _, _, _, _) = Create();

        await coordinator.StopAllAsync();
        var exception = await Record.ExceptionAsync(() => coordinator.StopAllAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task StopAllAsync_Logs_Start_And_Completion()
    {
        var (coordinator, _, _, _, _, _, logger) = Create();

        await coordinator.StopAllAsync();

        Assert.Contains(logger.InformationMessages, m => m.Contains("Acil durdurma başlatıldı"));
        Assert.Contains(logger.InformationMessages, m => m.Contains("Acil durdurma tamamlandı"));
    }
}
