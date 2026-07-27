using MertClicker.Application.Services;
using MertClicker.Application.Tests.Fakes;
using MertClicker.Domain;
using MertClicker.Domain.Automation;
using MertClicker.Domain.Macros;

namespace MertClicker.Application.Tests.Services;

public class AutomationEngineTests
{
    private static (AutomationEngine Engine, FakeInputInjector Injector) CreateEngine()
    {
        var injector = new FakeInputInjector();
        var clock = new RealElapsedClock();
        var scheduler = new PlaybackScheduler(clock);
        var logger = new FakeApplicationLogger();
        var engine = new AutomationEngine(injector, clock, scheduler, logger);
        return (engine, injector);
    }

    private static IReadOnlyList<MacroAction> SimpleClickTemplate(long intervalTicks) =>
    [
        new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left },
        new MouseButtonUpAction { OffsetTicks = 0, Button = MouseButton.Left },
        new DelayAction { OffsetTicks = 0, DurationTicks = intervalTicks }
    ];

    [Fact]
    public async Task RunAsync_With_FixedCount_Executes_Requested_Number_Of_Iterations()
    {
        var (engine, injector) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.005 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 3 };

        var result = await engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(9, result.ExecutedActionCount);
        Assert.Equal(3, injector.PressedButtons.Count);
        Assert.Equal(3, injector.ReleasedButtons.Count);
        Assert.Equal(PlaybackState.Idle, engine.State);
    }

    [Fact]
    public async Task RunAsync_With_UntilStopped_Runs_Until_StopAsync_Is_Called()
    {
        var (engine, injector) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.005 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions { RepeatMode = RepeatMode.UntilStopped };

        var runTask = engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);

        await Task.Delay(50);
        await engine.StopAsync();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.True(injector.PressedButtons.Count > 0);
        Assert.Equal(injector.PressedButtons.Count, injector.ReleasedButtons.Count);
        Assert.Equal(PlaybackState.Idle, engine.State);
    }

    [Fact]
    public async Task RunAsync_Honors_StartDelay_Before_First_Action()
    {
        var (engine, injector) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.005 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions
        {
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = 1,
            StartDelay = TimeSpan.FromMilliseconds(60)
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed.TotalMilliseconds >= 50, $"Beklenen en az ~60ms, ölçülen: {sw.Elapsed.TotalMilliseconds}ms");
        Assert.Single(injector.PressedButtons);
    }

    [Fact]
    public async Task RunAsync_Honors_Pause_During_StartDelay()
    {
        var (engine, injector) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.005 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions
        {
            RepeatMode = RepeatMode.FixedCount,
            RepeatCount = 1,
            StartDelay = TimeSpan.FromMilliseconds(30)
        };

        var runTask = engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);

        // StartDelay (30ms) tamamlanmadan hemen duraklat.
        await engine.PauseAsync();

        // StartDelay'in kendi başına doğal olarak tamamlanmış olacağı süreden (30ms) fazlasını
        // bekle; duraklatma StartDelay sırasında da dikkate alınıyorsa bu sürede hiçbir eylem
        // çalışmamalı.
        await Task.Delay(80);
        Assert.Empty(injector.PressedButtons);

        await engine.ResumeAsync();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.Single(injector.PressedButtons);
    }

    [Fact]
    public async Task RunAsync_Throws_When_Already_Running()
    {
        var (engine, _) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.05 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions { RepeatMode = RepeatMode.UntilStopped };

        var runTask = engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);
        await Task.Delay(10);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None));

        await engine.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task RunAsync_Guards_Truly_Concurrent_Calls_From_Different_Threads()
    {
        // Eski koruma (check-then-act üzerinde çalışan bir "State" alanı) atomik değildi; iki farklı
        // thread'den (ör. AutoClickerService ile MacroPlayer'ın kendi bağımsız kapılarından, F6 ve
        // F8 neredeyse aynı anda tetiklendiğinde) neredeyse aynı anda çağrılan RunAsync ikisi de
        // "Idle" görüp içeri girebilirdi. Bu test, gerçek thread pool thread'lerinden gelen 20
        // gerçekten eşzamanlı çağrıdan yalnızca BİRİNİN kabul edildiğini doğrular.
        var (engine, injector) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.05 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions { RepeatMode = RepeatMode.UntilStopped };

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None)))
            .ToArray();

        await Task.Delay(30);
        await engine.StopAsync();

        var results = await Task.WhenAll(tasks.Select(async t =>
        {
            try
            {
                return await t;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }));

        var acceptedResults = results.Where(r => r is not null).ToList();
        Assert.Single(acceptedResults);
        Assert.True(acceptedResults[0]!.Success);
        Assert.Equal(PlaybackState.Idle, engine.State);
    }

    [Fact]
    public async Task RunAsync_Releases_Pressed_Buttons_When_A_Later_Action_Fails()
    {
        // Fare tuşu başarıyla basılı hale getirildikten sonra sonraki eylem hata verirse,
        // motor basılı kalan tuşu güvenlik amacıyla bırakmalıdır.
        var injector = new FailsOnScrollInputInjector();
        var clock = new RealElapsedClock();
        var scheduler = new PlaybackScheduler(clock);
        var logger = new FakeApplicationLogger();
        var engine = new AutomationEngine(injector, clock, scheduler, logger);

        var actions = new List<MacroAction>
        {
            new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left },
            new MouseWheelAction { OffsetTicks = 0, Delta = 1 }
        };
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var result = await engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PlaybackState.Faulted, engine.State);
        Assert.True(injector.ReleaseCalledAfterFailure);
    }

    [Fact]
    public async Task RunAsync_Executes_Key_Actions_Via_The_Input_Injector()
    {
        var (engine, injector) = CreateEngine();
        var actions = new List<MacroAction>
        {
            new KeyDownAction { OffsetTicks = 0, KeyCode = 0x41 },
            new KeyUpAction { OffsetTicks = 0, KeyCode = 0x41 }
        };
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var result = await engine.RunAsync(actions, options, PlaybackState.Playing, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([(ushort)0x41], injector.PressedKeys);
        Assert.Equal([(ushort)0x41], injector.ReleasedKeys);
    }

    [Fact]
    public async Task RunAsync_Releases_Held_Keys_When_Stopped_Mid_Iteration()
    {
        var (engine, injector) = CreateEngine();
        var actions = new List<MacroAction>
        {
            new KeyDownAction { OffsetTicks = 0, KeyCode = 0x41 },
            new DelayAction { OffsetTicks = 0, DurationTicks = (long)(0.5 * System.Diagnostics.Stopwatch.Frequency) },
            new KeyUpAction { OffsetTicks = (long)(0.5 * System.Diagnostics.Stopwatch.Frequency), KeyCode = 0x41 }
        };
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var runTask = engine.RunAsync(actions, options, PlaybackState.Playing, CancellationToken.None);

        await Task.Delay(30);
        await engine.StopAsync();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.Contains((ushort)0x41, injector.PressedKeys);
        Assert.Contains((ushort)0x41, injector.ReleasedKeys);
    }

    [Fact]
    public async Task PauseAsync_Blocks_Progress_Until_ResumeAsync_Is_Called()
    {
        var (engine, injector) = CreateEngine();
        var actions = SimpleClickTemplate(intervalTicks: (long)(0.005 * System.Diagnostics.Stopwatch.Frequency));
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 5 };

        var runTask = engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);

        await Task.Delay(10);
        await engine.PauseAsync();
        Assert.Equal(PlaybackState.Paused, engine.State);

        var pressedCountWhilePaused = injector.PressedButtons.Count;
        await Task.Delay(60);
        Assert.Equal(pressedCountWhilePaused, injector.PressedButtons.Count);

        await engine.ResumeAsync();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.Equal(5, injector.PressedButtons.Count);
    }

    [Fact]
    public async Task PauseAsync_Compensates_Elapsed_Real_Time_For_Actions_After_Resume()
    {
        // Kayıttaki göreli zamanlama: action0 @ 0ms, action1 (duraklatma noktası) @ ~30ms,
        // action2 @ ~130ms (action1'den 100ms sonra). PauseAsync() erkenden (t≈0) çağrılır ama motor
        // bunu ancak action1'in ~30ms'lik bekleyişi doğal olarak tamamlanınca fark eder; asıl
        // duraklatma bloğu o andan itibaren başlar. Resume, testin başlangıcından ~300ms sonra
        // çağrılır — yani gerçek duraklatma bloğu (~270ms), action1→action2 arasındaki 100ms'lik
        // farktan uzun sürer ve resume anındaki gerçek zaman (300ms) action2'nin ORİJİNAL hedefini
        // (130ms) çoktan geçmiş olur. Telafi YOKSA action2 resume'dan hemen sonra (~0ms) ateşlenir
        // (hedef zaten geçilmiş); telafi VARSA hedef ileri kaydığı için resume'dan ~100ms sonra
        // ateşlenir. Bu iki senaryo arasındaki fark, zamanlama/planlama gürültüsüyle karışmayacak
        // kadar büyük.
        var (engine, injector) = CreateEngine();
        var frequency = System.Diagnostics.Stopwatch.Frequency;
        var actions = new List<MacroAction>
        {
            new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left },
            new MouseButtonUpAction { OffsetTicks = (long)(0.03 * frequency), Button = MouseButton.Left },
            new MouseWheelAction { OffsetTicks = (long)(0.13 * frequency), Delta = 1 }
        };
        var options = new PlaybackOptions { RepeatMode = RepeatMode.FixedCount, RepeatCount = 1 };

        var testStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var runTask = engine.RunAsync(actions, options, PlaybackState.Clicking, CancellationToken.None);

        // PauseAsync() State'i hemen "Paused" yapar (motorun bunu fiilen fark etmesini beklemeden);
        // gerçek blok, motor action1'in bekleyişini tamamlayıp duraklatma kapısına takıldığında başlar.
        await engine.PauseAsync();

        // Testin başlangıcından itibaren toplam ~300ms geçmesini bekle (gerçek duraklatma süresini
        // simüle eder). Bu süre, action1→action2 arasındaki 100ms'lik farktan belirgin şekilde uzun.
        var remaining = TimeSpan.FromMilliseconds(300) - testStopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        var resumeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await engine.ResumeAsync();

        await WaitUntilAsync(() => injector.ScrolledDeltas.Count > 0, timeoutMs: 2000);
        resumeStopwatch.Stop();

        await runTask;

        Assert.True(
            resumeStopwatch.ElapsedMilliseconds >= 40,
            $"action2, resume'dan yalnızca {resumeStopwatch.ElapsedMilliseconds}ms sonra tetiklendi; " +
            "duraklatma süresi telafi edilmemiş olabilir (beklenen: ~100ms).");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Koşul zaman aşımına uğradı.");
            }

            await Task.Delay(2);
        }
    }

    private sealed class FailsOnScrollInputInjector : MertClicker.Application.Abstractions.IInputInjector
    {
        public bool ReleaseCalledAfterFailure { get; private set; }

        public MertClicker.Application.Models.InputInjectionResult MoveMouse(MertClicker.Domain.Display.ScreenPoint point) =>
            new(true, 1, 1, null, null);

        public MertClicker.Application.Models.InputInjectionResult MouseDown(MouseButton button) =>
            new(true, 1, 1, null, null);

        public MertClicker.Application.Models.InputInjectionResult MouseUp(MouseButton button)
        {
            ReleaseCalledAfterFailure = true;
            return new MertClicker.Application.Models.InputInjectionResult(true, 1, 1, null, null);
        }

        public MertClicker.Application.Models.InputInjectionResult Scroll(int delta) =>
            new(false, 1, 0, 1, "Test hatası: SendInput engellendi.");

        public MertClicker.Application.Models.InputInjectionResult KeyDown(ushort keyCode) =>
            new(true, 1, 1, null, null);

        public MertClicker.Application.Models.InputInjectionResult KeyUp(ushort keyCode) =>
            new(true, 1, 1, null, null);
    }
}
