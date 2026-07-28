using m4AutoClicker.Application.Services;
using m4AutoClicker.Application.Tests.Fakes;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Tests.Services;

public class MacroPlayerTests
{
    private static (MacroPlayer Player, FakeInputInjector Injector, FakeDisplayService DisplayService) CreatePlayer()
    {
        var injector = new FakeInputInjector();
        var clock = new RealElapsedClock();
        var scheduler = new PlaybackScheduler(clock);
        var logger = new FakeApplicationLogger();
        var engine = new AutomationEngine(injector, clock, scheduler, logger);
        var displayService = new FakeDisplayService();

        return (new MacroPlayer(engine, displayService, logger), injector, displayService);
    }

    // FakeDisplayService'in varsayılan anlık görüntüsüyle (1920x1080, 1 monitör) eşleşir; böylece
    // mevcut testlerde beklenmeyen bir DisplayMismatchWarning oluşmaz.
    private static Macro CreateMacro(IReadOnlyList<MacroAction> actions) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Makro",
        SchemaVersion = 1,
        CreatedAtUtc = DateTime.UnixEpoch,
        UpdatedAtUtc = DateTime.UnixEpoch,
        DurationTicks = 0,
        DisplaySnapshot = new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = [new MonitorSnapshot { DeviceId = "PRIMARY", Bounds = new MonitorBounds(0, 0, 1920, 1080), WorkingArea = new MonitorBounds(0, 0, 1920, 1040), DpiX = 96, DpiY = 96, IsPrimary = true }]
        },
        Actions = actions
    };

    private static PlaybackOptions OnceOptions() => new() { SpeedMultiplier = 1.0, RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

    [Fact]
    public async Task PlayAsync_Executes_Recorded_Actions_In_Order()
    {
        var (player, injector, _) = CreatePlayer();
        var macro = CreateMacro(
        [
            new MouseMoveAction { OffsetTicks = 0, X = 100, Y = 200 },
            new MouseButtonDownAction { OffsetTicks = 5, Button = MouseButton.Left },
            new MouseButtonUpAction { OffsetTicks = 10, Button = MouseButton.Left }
        ]);

        var result = await player.PlayAsync(macro, OnceOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.ExecutedActionCount);
        Assert.Single(injector.MovedTo);
        Assert.Equal(new ScreenPoint(100, 200), injector.MovedTo[0]);
        Assert.Single(injector.PressedButtons);
        Assert.Single(injector.ReleasedButtons);
    }

    [Fact]
    public async Task PlayAsync_Sets_DisplayMismatchWarning_When_Resolution_Differs_From_Recording()
    {
        var (player, _, displayService) = CreatePlayer();
        var macro = CreateMacro([new MouseMoveAction { OffsetTicks = 0, X = 10, Y = 10 }]);
        displayService.Snapshot = displayService.Snapshot with { VirtualWidth = 2560, VirtualHeight = 1440 };

        var result = await player.PlayAsync(macro, OnceOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.DisplayMismatchWarning);
    }

    [Fact]
    public async Task PlayAsync_Leaves_DisplayMismatchWarning_Null_When_Resolution_Matches_Recording()
    {
        var (player, _, _) = CreatePlayer();
        var macro = CreateMacro([new MouseMoveAction { OffsetTicks = 0, X = 10, Y = 10 }]);

        var result = await player.PlayAsync(macro, OnceOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.DisplayMismatchWarning);
    }

    [Fact]
    public async Task PlayAsync_On_Empty_Macro_Succeeds_With_Zero_Actions()
    {
        var (player, _, _) = CreatePlayer();
        var macro = CreateMacro([]);

        var result = await player.PlayAsync(macro, OnceOptions(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExecutedActionCount);
    }

    [Fact]
    public async Task PlayAsync_Second_Concurrent_Call_Fails_Gracefully_Instead_Of_Throwing()
    {
        var macro = CreateMacro([new DelayAction { OffsetTicks = 0, DurationTicks = (long)(0.05 * System.Diagnostics.Stopwatch.Frequency) }]);
        var (player, _, _) = CreatePlayer();
        var options = new PlaybackOptions { RepeatMode = RepeatMode.UntilStopped };

        var firstTask = player.PlayAsync(macro, options, CancellationToken.None);
        var secondTask = player.PlayAsync(macro, options, CancellationToken.None);

        var secondResult = await secondTask;
        Assert.False(secondResult.Success);
        Assert.Contains("zaten çalışıyor", secondResult.ErrorMessage);

        await player.StopAsync();
        var firstResult = await firstTask;
        Assert.True(firstResult.Success);
    }

    [Fact]
    public async Task StopAsync_Stops_An_UntilStopped_Playback()
    {
        var (player, injector, _) = CreatePlayer();
        var macro = CreateMacro(
        [
            new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left },
            new MouseButtonUpAction { OffsetTicks = 5, Button = MouseButton.Left },
            new DelayAction { OffsetTicks = 5, DurationTicks = (long)(0.005 * System.Diagnostics.Stopwatch.Frequency) }
        ]);
        var options = new PlaybackOptions { RepeatMode = RepeatMode.UntilStopped };

        var playTask = player.PlayAsync(macro, options, CancellationToken.None);
        await Task.Delay(30);

        await player.StopAsync();
        var result = await playTask;

        Assert.True(result.Success);
        Assert.True(injector.PressedButtons.Count > 0);
        Assert.Equal(injector.PressedButtons.Count, injector.ReleasedButtons.Count);
    }

    [Fact]
    public void State_Reflects_Idle_Before_Playing()
    {
        var (player, _, _) = CreatePlayer();

        Assert.Equal(PlaybackState.Idle, player.State);
    }

    [Fact]
    public async Task PauseAsync_And_ResumeAsync_Raise_PlaybackPausedChanged()
    {
        // Birden fazla ViewModel (Makrolarım ve Makro Kaydedici) aynı IMacroPlayer singleton'ına
        // abone olup yerel IsPlaybackPaused durumlarını bu olaydan senkronize eder; olay
        // ateşlenmezse biri diğerinin duraklattığı oynatmayı fark edemez.
        var (player, _, _) = CreatePlayer();
        var macro = CreateMacro(
        [
            new DelayAction { OffsetTicks = 0, DurationTicks = (long)(0.2 * System.Diagnostics.Stopwatch.Frequency) }
        ]);
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var raisedValues = new List<bool>();
        player.PlaybackPausedChanged += (_, isPaused) => raisedValues.Add(isPaused);

        var playTask = player.PlayAsync(macro, options, CancellationToken.None);
        await Task.Delay(20);

        await player.PauseAsync();
        await player.ResumeAsync();
        await player.StopAsync();
        await playTask;

        Assert.Equal([true, false], raisedValues);
    }

    [Fact]
    public async Task PauseAsync_And_ResumeAsync_Delegate_To_Engine_Without_Throwing()
    {
        var (player, _, _) = CreatePlayer();
        var macro = CreateMacro(
        [
            new DelayAction { OffsetTicks = 0, DurationTicks = (long)(0.2 * System.Diagnostics.Stopwatch.Frequency) }
        ]);
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var playTask = player.PlayAsync(macro, options, CancellationToken.None);
        await Task.Delay(20);

        await player.PauseAsync();
        Assert.Equal(PlaybackState.Paused, player.State);

        await player.ResumeAsync();
        await player.StopAsync();
        await playTask;
    }
}
