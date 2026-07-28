using m4AutoClicker.Application.Models;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Application.Tests.Fakes;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Tests.Services;

public class MacroOptimizerTests
{
    private static Macro CreateMacro(IReadOnlyList<MacroAction> actions) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Makro",
        SchemaVersion = 1,
        CreatedAtUtc = DateTime.UnixEpoch,
        UpdatedAtUtc = DateTime.UnixEpoch,
        DurationTicks = 1000,
        DisplaySnapshot = new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = []
        },
        Actions = actions
    };

    // Frequency=1000 -> 1 tick = 1ms, testlerde okunması kolay olsun diye.
    private static MacroOptimizer CreateOptimizer(int minIntervalMs = 8, int minDistancePixels = 2)
    {
        var settingsProvider = new FakeApplicationSettingsProvider
        {
            Current = new ApplicationSettings
            {
                MouseMovementSampling = new MouseMovementSamplingSettings
                {
                    MinimumIntervalMilliseconds = minIntervalMs, MinimumDistancePixels = minDistancePixels
                }
            }
        };

        return new MacroOptimizer(new FakeHighResolutionClock { Frequency = 1000 }, settingsProvider);
    }

    [Fact]
    public void Optimize_Keeps_Non_Move_Actions_Unchanged()
    {
        var optimizer = CreateOptimizer();
        var macro = CreateMacro(
        [
            new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left },
            new MouseButtonUpAction { OffsetTicks = 30, Button = MouseButton.Left },
            new MouseWheelAction { OffsetTicks = 60, Delta = 120 }
        ]);

        var result = optimizer.Optimize(macro);

        Assert.Equal(3, result.Actions.Count);
        Assert.IsType<MouseButtonDownAction>(result.Actions[0]);
        Assert.IsType<MouseButtonUpAction>(result.Actions[1]);
        Assert.IsType<MouseWheelAction>(result.Actions[2]);
    }

    [Fact]
    public void Optimize_Drops_Moves_Too_Close_In_Time_And_Distance()
    {
        var optimizer = CreateOptimizer(minIntervalMs: 8, minDistancePixels: 2);
        var macro = CreateMacro(
        [
            new MouseMoveAction { OffsetTicks = 0, X = 100, Y = 100 },
            new MouseMoveAction { OffsetTicks = 2, X = 101, Y = 100 }, // çok yakın zaman + mesafe -> atlanır
            new MouseMoveAction { OffsetTicks = 4, X = 101, Y = 101 }, // yine çok yakın -> atlanır
            new MouseMoveAction { OffsetTicks = 50, X = 200, Y = 200 } // yeterince uzak -> tutulur
        ]);

        var result = optimizer.Optimize(macro);

        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(new ScreenPointLike(100, 100), ToPoint(result.Actions[0]));
        Assert.Equal(new ScreenPointLike(200, 200), ToPoint(result.Actions[1]));
    }

    [Fact]
    public void Optimize_Keeps_Move_By_Distance_Even_If_Time_Threshold_Not_Reached()
    {
        var optimizer = CreateOptimizer(minIntervalMs: 100, minDistancePixels: 5);
        var macro = CreateMacro(
        [
            new MouseMoveAction { OffsetTicks = 0, X = 0, Y = 0 },
            new MouseMoveAction { OffsetTicks = 1, X = 50, Y = 50 } // zaman eşiği geçmedi ama mesafe çok büyük -> tutulur
        ]);

        var result = optimizer.Optimize(macro);

        Assert.Equal(2, result.Actions.Count);
    }

    [Fact]
    public void Optimize_Always_Keeps_Move_Immediately_Before_A_Click()
    {
        var optimizer = CreateOptimizer(minIntervalMs: 100, minDistancePixels: 100);
        var macro = CreateMacro(
        [
            new MouseMoveAction { OffsetTicks = 0, X = 0, Y = 0 },
            new MouseMoveAction { OffsetTicks = 1, X = 5, Y = 5 }, // eşikleri geçmiyor, normalde atlanır
            new MouseButtonDownAction { OffsetTicks = 2, Button = MouseButton.Left }
        ]);

        var result = optimizer.Optimize(macro);

        // Tıklamadan hemen önceki gerçek imleç konumu (5,5) korunmalı; aksi halde tıklama
        // playback sırasında yanlış konumda gerçekleşir.
        Assert.Equal(3, result.Actions.Count);
        Assert.Equal(new ScreenPointLike(5, 5), ToPoint(result.Actions[1]));
        Assert.IsType<MouseButtonDownAction>(result.Actions[2]);
    }

    [Fact]
    public void Optimize_Keeps_Last_Move_At_End_Of_Recording_Even_If_Filtered()
    {
        var optimizer = CreateOptimizer(minIntervalMs: 100, minDistancePixels: 100);
        var macro = CreateMacro(
        [
            new MouseMoveAction { OffsetTicks = 0, X = 0, Y = 0 },
            new MouseMoveAction { OffsetTicks = 1, X = 3, Y = 3 } // eşikleri geçmiyor ama kayıt burada bitiyor
        ]);

        var result = optimizer.Optimize(macro);

        Assert.Equal(2, result.Actions.Count);
        Assert.Equal(new ScreenPointLike(3, 3), ToPoint(result.Actions[1]));
    }

    [Fact]
    public void Optimize_Preserves_Macro_Metadata()
    {
        var optimizer = CreateOptimizer();
        var macro = CreateMacro([new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left }]);

        var result = optimizer.Optimize(macro);

        Assert.Equal(macro.Id, result.Id);
        Assert.Equal(macro.Name, result.Name);
        Assert.Equal(macro.DurationTicks, result.DurationTicks);
    }

    private static ScreenPointLike ToPoint(MacroAction action)
    {
        var move = Assert.IsType<MouseMoveAction>(action);
        return new ScreenPointLike(move.X, move.Y);
    }

    private readonly record struct ScreenPointLike(int X, int Y);

    [Fact]
    public void Optimize_Reads_Sampling_Settings_Freshly_On_Each_Call()
    {
        // Ayarlar ekranından yapılan bir değişikliğin, uygulama yeniden başlatılmadan bir sonraki
        // kayıtta etkili olması gerekir; bu yüzden MacroOptimizer ayarları constructor'da değil,
        // Optimize() her çağrıldığında sağlayıcıdan okumalı.
        var clock = new FakeHighResolutionClock { Frequency = 1000 };
        var settingsProvider = new FakeApplicationSettingsProvider
        {
            Current = new ApplicationSettings
            {
                MouseMovementSampling = new MouseMovementSamplingSettings { MinimumIntervalMilliseconds = 100, MinimumDistancePixels = 100 }
            }
        };
        var optimizer = new MacroOptimizer(clock, settingsProvider);

        // Ortadaki hareket, hem yeterince yakın (seyreltilebilir) hem de kaydın SONU olmadığı için
        // (üçüncü bir hareketle geçersiz kılınıyor) yüksek eşiklerde tamamen elenir.
        var macro = CreateMacro(
        [
            new MouseMoveAction { OffsetTicks = 0, X = 0, Y = 0 },
            new MouseMoveAction { OffsetTicks = 1, X = 5, Y = 5 },
            new MouseMoveAction { OffsetTicks = 200, X = 50, Y = 50 }
        ]);

        var firstResult = optimizer.Optimize(macro);
        Assert.Equal(2, firstResult.Actions.Count); // yüksek eşikler nedeniyle ortadaki hareket elenir

        settingsProvider.Current = new ApplicationSettings
        {
            MouseMovementSampling = new MouseMovementSamplingSettings { MinimumIntervalMilliseconds = 0, MinimumDistancePixels = 0 }
        };

        var secondResult = optimizer.Optimize(macro);
        Assert.Equal(3, secondResult.Actions.Count); // eşikler sıfırlanınca artık hiçbir şey atlanmaz
    }
}
