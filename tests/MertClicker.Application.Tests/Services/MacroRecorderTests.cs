using System.Threading.Channels;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using MertClicker.Application.Services;
using MertClicker.Application.Tests.Fakes;
using MertClicker.Domain;
using MertClicker.Domain.Automation;
using MertClicker.Domain.Macros;

namespace MertClicker.Application.Tests.Services;

public class MacroRecorderTests
{
    // Eylemleri seyreltmeden ham hâliyle test edebilmek için eşikleri devre dışı bırakacak kadar
    // yüksek bir aralık kullanmıyoruz; bunun yerine mesafe/zaman farkları test verisinde eşiklerin
    // altında kalmayacak şekilde ayarlanıyor (varsayılan: 8ms / 2px).
    private static (MacroRecorder Recorder, FakeInputCaptureProvider Capture, FakeKeyboardCaptureProvider KeyboardCapture, FakeHighResolutionClock Clock, FakeApplicationLogger Logger) CreateRecorder()
    {
        var capture = new FakeInputCaptureProvider();
        var keyboardCapture = new FakeKeyboardCaptureProvider();
        var clock = new FakeHighResolutionClock { Frequency = 1000 }; // 1 tick = 1ms
        var displayService = new FakeDisplayService();
        var logger = new FakeApplicationLogger();
        var optimizer = new MacroOptimizer(clock, new FakeApplicationSettingsProvider());

        var recorder = new MacroRecorder(capture, keyboardCapture, clock, displayService, optimizer, logger);
        return (recorder, capture, keyboardCapture, clock, logger);
    }

    [Fact]
    public async Task StartAsync_Sets_State_To_Recording_And_Starts_Capture()
    {
        var (recorder, capture, keyboardCapture, _, _) = CreateRecorder();

        await recorder.StartAsync(CancellationToken.None);

        Assert.Equal(RecordingState.Recording, recorder.State);
        Assert.True(capture.IsCapturing);
        Assert.True(keyboardCapture.IsCapturing);

        await recorder.StopAsync();
    }

    [Fact]
    public async Task StartAsync_Throws_When_Already_Recording()
    {
        var (recorder, _, _, _, _) = CreateRecorder();
        await recorder.StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.StartAsync(CancellationToken.None));

        await recorder.StopAsync();
    }

    [Fact]
    public async Task StopAsync_Throws_When_Not_Recording()
    {
        var (recorder, _, _, _, _) = CreateRecorder();

        await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.StopAsync());
    }

    [Fact]
    public async Task StopAsync_Converts_Captured_Events_Into_Macro_Actions_With_Relative_Offsets()
    {
        var (recorder, capture, _, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 1000;

        await recorder.StartAsync(CancellationToken.None);

        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.Move, X = 10, Y = 20, TimestampTicks = 1000, IsInjectedByApplication = false
        });
        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonDown, X = 10, Y = 20, TimestampTicks = 1050, IsInjectedByApplication = false
        });
        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonUp, X = 10, Y = 20, TimestampTicks = 1080, IsInjectedByApplication = false
        });
        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.Wheel, X = 10, Y = 20, WheelDelta = 120, TimestampTicks = 1120, IsInjectedByApplication = false
        });

        var macro = await recorder.StopAsync();

        Assert.Equal(4, macro.Actions.Count);
        Assert.Equal(0, macro.Actions[0].OffsetTicks);
        Assert.IsType<MouseMoveAction>(macro.Actions[0]);
        Assert.Equal(50, macro.Actions[1].OffsetTicks);
        Assert.IsType<MouseButtonDownAction>(macro.Actions[1]);
        Assert.Equal(80, macro.Actions[2].OffsetTicks);
        Assert.IsType<MouseButtonUpAction>(macro.Actions[2]);
        Assert.Equal(120, macro.Actions[3].OffsetTicks);
        Assert.IsType<MouseWheelAction>(macro.Actions[3]);
        Assert.Equal(120, ((MouseWheelAction)macro.Actions[3]).Delta);
    }

    [Fact]
    public async Task StopAsync_Filters_Out_Application_Injected_Events()
    {
        var (recorder, capture, _, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None);

        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonDown, X = 1, Y = 1, TimestampTicks = 10, IsInjectedByApplication = true
        });
        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonUp, X = 1, Y = 1, TimestampTicks = 20, IsInjectedByApplication = false
        });

        var macro = await recorder.StopAsync();

        Assert.Single(macro.Actions);
        Assert.IsType<MouseButtonUpAction>(macro.Actions[0]);
    }

    [Fact]
    public async Task StopAsync_Sets_State_Back_To_Idle_And_Stops_Capture()
    {
        var (recorder, capture, keyboardCapture, _, _) = CreateRecorder();
        await recorder.StartAsync(CancellationToken.None);

        await recorder.StopAsync();

        Assert.Equal(RecordingState.Idle, recorder.State);
        Assert.False(capture.IsCapturing);
        Assert.False(keyboardCapture.IsCapturing);
    }

    [Fact]
    public async Task Recorder_Can_Start_Again_After_A_Completed_Recording()
    {
        var (recorder, _, _, _, _) = CreateRecorder();

        await recorder.StartAsync(CancellationToken.None);
        await recorder.StopAsync();

        var exception = await Record.ExceptionAsync(() => recorder.StartAsync(CancellationToken.None));
        Assert.Null(exception);
        Assert.Equal(RecordingState.Recording, recorder.State);

        await recorder.StopAsync();
    }

    [Fact]
    public async Task StopAsync_Applies_Optimizer_To_Thin_Dense_Moves()
    {
        var (recorder, capture, _, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None);

        // Varsayılan örnekleme eşiklerinin (8ms/2px) çok altında, birbirine yapışık 10 hareket.
        for (var i = 0; i < 10; i++)
        {
            await capture.Writer.WriteAsync(new RawMouseEvent
            {
                EventType = RawMouseEventType.Move, X = i, Y = 0, TimestampTicks = i, IsInjectedByApplication = false
            });
        }

        var macro = await recorder.StopAsync();

        Assert.True(macro.Actions.Count < 10, $"Optimize sonrası eylem sayısı {macro.Actions.Count}, azaltılması bekleniyordu.");
    }

    [Fact]
    public async Task StopAsync_Converts_Captured_Keyboard_Events_Into_Macro_Actions()
    {
        var (recorder, _, keyboardCapture, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 1000;

        await recorder.StartAsync(CancellationToken.None);

        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x41, TimestampTicks = 1010, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x41, TimestampTicks = 1040, IsInjectedByApplication = false
        });

        var macro = await recorder.StopAsync();

        Assert.Equal(2, macro.Actions.Count);
        Assert.Equal(10, macro.Actions[0].OffsetTicks);
        var down = Assert.IsType<KeyDownAction>(macro.Actions[0]);
        Assert.Equal((ushort)0x41, down.KeyCode);
        Assert.Equal(40, macro.Actions[1].OffsetTicks);
        var up = Assert.IsType<KeyUpAction>(macro.Actions[1]);
        Assert.Equal((ushort)0x41, up.KeyCode);
    }

    [Fact]
    public async Task StopAsync_Filters_Out_Application_Injected_Keyboard_Events()
    {
        var (recorder, _, keyboardCapture, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None);

        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x41, TimestampTicks = 10, IsInjectedByApplication = true
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x41, TimestampTicks = 20, IsInjectedByApplication = false
        });

        var macro = await recorder.StopAsync();

        Assert.Single(macro.Actions);
        Assert.IsType<KeyUpAction>(macro.Actions[0]);
    }

    [Fact]
    public async Task StopAsync_Merges_Mouse_And_Keyboard_Events_In_Chronological_Order()
    {
        var (recorder, capture, keyboardCapture, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None);

        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonDown, X = 1, Y = 1, TimestampTicks = 30, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x41, TimestampTicks = 10, IsInjectedByApplication = false
        });
        await capture.Writer.WriteAsync(new RawMouseEvent
        {
            EventType = RawMouseEventType.LeftButtonUp, X = 1, Y = 1, TimestampTicks = 50, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x41, TimestampTicks = 20, IsInjectedByApplication = false
        });

        var macro = await recorder.StopAsync();

        Assert.Equal(4, macro.Actions.Count);
        Assert.IsType<KeyDownAction>(macro.Actions[0]);
        Assert.Equal(10, macro.Actions[0].OffsetTicks);
        Assert.IsType<KeyUpAction>(macro.Actions[1]);
        Assert.Equal(20, macro.Actions[1].OffsetTicks);
        Assert.IsType<MouseButtonDownAction>(macro.Actions[2]);
        Assert.Equal(30, macro.Actions[2].OffsetTicks);
        Assert.IsType<MouseButtonUpAction>(macro.Actions[3]);
        Assert.Equal(50, macro.Actions[3].OffsetTicks);
    }

    [Fact]
    public async Task StopAsync_Excludes_Trailing_Key_Events_Matching_The_Stop_Trigger_Key()
    {
        // WH_KEYBOARD_LL kancası aktifken kaydı durduran kısayolun (ör. F7) kendi fiziksel basışı da
        // yakalanır; StopAsync bu tuşu son bir zaman penceresinde açıkça hariç tutabilmelidir.
        var (recorder, _, keyboardCapture, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None);

        // Kayıt sırasında bilerek basılan bir tuş (F7 değil) korunmalı.
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x41, TimestampTicks = 100, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x41, TimestampTicks = 120, IsInjectedByApplication = false
        });

        // Durdurma anına çok yakın gelen F7 basma/bırakma olayları (kısayolun kendisi).
        clock.CurrentTimestamp = 995;
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x76, TimestampTicks = 995, IsInjectedByApplication = false
        });
        clock.CurrentTimestamp = 998;
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x76, TimestampTicks = 998, IsInjectedByApplication = false
        });

        clock.CurrentTimestamp = 1000;
        var macro = await recorder.StopAsync(excludeTrailingKeyCodes: [0x76]);

        Assert.Equal(2, macro.Actions.Count);
        var down = Assert.IsType<KeyDownAction>(macro.Actions[0]);
        Assert.Equal((ushort)0x41, down.KeyCode);
        var up = Assert.IsType<KeyUpAction>(macro.Actions[1]);
        Assert.Equal((ushort)0x41, up.KeyCode);
    }

    [Fact]
    public async Task StopAsync_Does_Not_Exclude_An_Earlier_Legitimate_Press_Of_The_Same_Key()
    {
        var (recorder, _, keyboardCapture, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None);

        // F7'ye erken bir noktada bilerek basılmış (kaydı durdurmakla ilgisi yok, ör. bir oyun kısayolu).
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x76, TimestampTicks = 50, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x76, TimestampTicks = 70, IsInjectedByApplication = false
        });

        clock.CurrentTimestamp = 1000;
        var macro = await recorder.StopAsync(excludeTrailingKeyCodes: [0x76]);

        Assert.Equal(2, macro.Actions.Count);
        Assert.IsType<KeyDownAction>(macro.Actions[0]);
        Assert.IsType<KeyUpAction>(macro.Actions[1]);
    }

    [Fact]
    public async Task StartAsync_With_ReservedKeyCodes_Filters_Them_For_The_Whole_Recording()
    {
        // F6/F8/F9 gibi diğer global kısayollar, kaydın DURDURULMASIYLA ilgisi olmadan, kaydın
        // herhangi bir anında yanlışlıkla basılırsa makro içeriğine karışmamalı.
        var (recorder, _, keyboardCapture, clock, _) = CreateRecorder();
        clock.CurrentTimestamp = 0;

        await recorder.StartAsync(CancellationToken.None, reservedKeyCodes: [0x78]); // F9

        // Kaydın hemen başında rezerve edilen tuşa (F9) basılıyor.
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x78, TimestampTicks = 10, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x78, TimestampTicks = 20, IsInjectedByApplication = false
        });

        // Rezerve edilmemiş bir tuşa (A) basılıyor; korunmalı.
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyDown, KeyCode = 0x41, TimestampTicks = 30, IsInjectedByApplication = false
        });
        await keyboardCapture.Writer.WriteAsync(new RawKeyboardEvent
        {
            EventType = RawKeyboardEventType.KeyUp, KeyCode = 0x41, TimestampTicks = 40, IsInjectedByApplication = false
        });

        var macro = await recorder.StopAsync();

        Assert.Equal(2, macro.Actions.Count);
        var down = Assert.IsType<KeyDownAction>(macro.Actions[0]);
        Assert.Equal((ushort)0x41, down.KeyCode);
        var up = Assert.IsType<KeyUpAction>(macro.Actions[1]);
        Assert.Equal((ushort)0x41, up.KeyCode);
    }

    [Fact]
    public async Task StartAsync_Leaves_State_Idle_And_Stops_Mouse_Capture_When_Keyboard_Capture_Fails_To_Start()
    {
        // Fare kancası başarıyla kurulup klavye kancası başarısız olursa, State "Recording" olarak
        // takılı kalmamalı ve fare kancası da tek başına kurulu bırakılmamalı.
        var capture = new FakeInputCaptureProvider();
        var keyboardCapture = new FailingKeyboardCaptureProvider();
        var clock = new FakeHighResolutionClock { Frequency = 1000 };
        var displayService = new FakeDisplayService();
        var logger = new FakeApplicationLogger();
        var optimizer = new MacroOptimizer(clock, new FakeApplicationSettingsProvider());
        var recorder = new MacroRecorder(capture, keyboardCapture, clock, displayService, optimizer, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.StartAsync(CancellationToken.None));

        Assert.Equal(RecordingState.Idle, recorder.State);
        Assert.False(capture.IsCapturing);

        // Başarısızlıktan sonra tekrar başlatılabilmeli (State takılı kalmamış).
        keyboardCapture.ShouldFail = false;
        var exception = await Record.ExceptionAsync(() => recorder.StartAsync(CancellationToken.None));
        Assert.Null(exception);
        Assert.Equal(RecordingState.Recording, recorder.State);

        await recorder.StopAsync();
    }

    private sealed class FailingKeyboardCaptureProvider : IKeyboardCaptureProvider
    {
        private readonly Channel<RawKeyboardEvent> _channel = Channel.CreateUnbounded<RawKeyboardEvent>();

        public bool ShouldFail { get; set; } = true;

        public bool IsCapturing { get; private set; }

        public ChannelReader<RawKeyboardEvent> Events => _channel.Reader;

        public void Start()
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("Test hatası: klavye kancası kurulamadı.");
            }

            IsCapturing = true;
        }

        public void Stop() => IsCapturing = false;

        public void Dispose() => _channel.Writer.TryComplete();
    }
}
