using m4AutoClicker.Application.Services;
using m4AutoClicker.Application.Tests.Fakes;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Tests.Services;

public class AutoClickerServiceTests
{
    private static (AutoClickerService Service, FakeInputInjector Injector) CreateService()
    {
        var injector = new FakeInputInjector();
        var clock = new RealElapsedClock();
        var scheduler = new PlaybackScheduler(clock);
        var logger = new FakeApplicationLogger();
        var engine = new AutomationEngine(injector, clock, scheduler, logger);
        var coordinateResolver = new CoordinateResolver();
        var displayService = new FakeDisplayService();

        var service = new AutoClickerService(engine, coordinateResolver, displayService, clock);
        return (service, injector);
    }

    [Fact]
    public async Task StartAsync_Fails_When_Interval_Is_Zero()
    {
        var (service, _) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.Zero,
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = 1,
            Target = CoordinateTarget.CurrentCursor
        };

        var result = await service.StartAsync(plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("aralığı", result.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_Fails_When_FixedCount_Missing_RepeatCount()
    {
        var (service, _) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(50),
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = null,
            Target = CoordinateTarget.CurrentCursor
        };

        var result = await service.StartAsync(plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("tekrar sayısı", result.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_Fails_When_FixedPoint_Target_Is_Outside_Virtual_Desktop()
    {
        var (service, _) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(50),
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = 1,
            Target = CoordinateTarget.FixedPoint(new ScreenPoint(99999, 99999))
        };

        var result = await service.StartAsync(plan, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_Clicks_Requested_Number_Of_Times_At_Fixed_Point()
    {
        var (service, injector) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(5),
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = 3,
            Target = CoordinateTarget.FixedPoint(new ScreenPoint(100, 200))
        };

        var result = await service.StartAsync(plan, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, injector.PressedButtons.Count);
        Assert.Equal(3, injector.ReleasedButtons.Count);
        Assert.Equal(3, injector.MovedTo.Count);
        Assert.All(injector.MovedTo, point => Assert.Equal(new ScreenPoint(100, 200), point));
    }

    [Fact]
    public async Task StartAsync_Double_Click_Presses_Button_Twice_Per_Iteration()
    {
        var (service, injector) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Double,
            Interval = TimeSpan.FromMilliseconds(5),
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = 1,
            Target = CoordinateTarget.CurrentCursor
        };

        var result = await service.StartAsync(plan, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, injector.PressedButtons.Count);
        Assert.Equal(2, injector.ReleasedButtons.Count);
        Assert.Empty(injector.MovedTo);
    }

    [Fact]
    public async Task StartAsync_Second_Concurrent_Call_Fails_Gracefully_Instead_Of_Throwing()
    {
        // F6 global kısayolu ile UI butonunun aynı anda tetiklenmesi durumunda AutomationEngine'in
        // "zaten çalışıyor" istisnası kullanıcıya sızmamalı; ikinci çağrı temiz bir hata sonucu almalı.
        var (service, injector) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(5),
            RepeatMode = RepeatMode.UntilStopped,
            Target = CoordinateTarget.CurrentCursor
        };

        var firstTask = service.StartAsync(plan, CancellationToken.None);
        var secondTask = service.StartAsync(plan, CancellationToken.None);

        var secondResult = await secondTask;
        Assert.False(secondResult.Success);
        Assert.Contains("zaten çalışıyor", secondResult.ErrorMessage);

        await service.StopAsync();
        var firstResult = await firstTask;

        Assert.True(firstResult.Success);
        Assert.True(injector.PressedButtons.Count > 0);
    }

    [Fact]
    public async Task StopAsync_Stops_An_UntilStopped_Run()
    {
        var (service, injector) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(5),
            RepeatMode = RepeatMode.UntilStopped,
            Target = CoordinateTarget.CurrentCursor
        };

        var runTask = service.StartAsync(plan, CancellationToken.None);

        await Task.Delay(30);
        await service.StopAsync();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.True(injector.PressedButtons.Count > 0);
        Assert.Equal(injector.PressedButtons.Count, injector.ReleasedButtons.Count);
    }

    [Fact]
    public async Task PauseAsync_And_ResumeAsync_Delegate_To_The_Underlying_Engine()
    {
        var (service, injector) = CreateService();
        var plan = new ClickPlan
        {
            Button = MouseButton.Left,
            ClickType = ClickType.Single,
            Interval = TimeSpan.FromMilliseconds(5),
            RepeatMode = RepeatMode.UntilStopped,
            Target = CoordinateTarget.CurrentCursor
        };

        var runTask = service.StartAsync(plan, CancellationToken.None);
        await Task.Delay(20);

        await service.PauseAsync();
        Assert.Equal(PlaybackState.Paused, service.State);

        var pressedCountWhilePaused = injector.PressedButtons.Count;
        await Task.Delay(40);
        Assert.Equal(pressedCountWhilePaused, injector.PressedButtons.Count);

        await service.ResumeAsync();
        Assert.NotEqual(PlaybackState.Paused, service.State);

        await service.StopAsync();
        var result = await runTask;

        Assert.True(result.Success);
    }
}
