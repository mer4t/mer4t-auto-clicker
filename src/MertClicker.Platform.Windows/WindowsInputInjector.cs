using System.Runtime.InteropServices;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using MertClicker.Domain;
using MertClicker.Domain.Display;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MertClicker.Platform.Windows;

public sealed class WindowsInputInjector : IInputInjector
{
    private readonly IApplicationLogger _logger;
    private readonly WindowsCoordinateConverter _coordinateConverter;

    public WindowsInputInjector(IApplicationLogger logger, WindowsCoordinateConverter coordinateConverter)
    {
        _logger = logger;
        _coordinateConverter = coordinateConverter;
    }

    public InputInjectionResult MoveMouse(ScreenPoint point)
    {
        var (x, y) = _coordinateConverter.ToVirtualDesktopNormalized(point);

        var input = CreateMouseInput(
            x,
            y,
            0,
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK);

        return Send(input);
    }

    public InputInjectionResult MouseDown(MouseButton button) =>
        Send(CreateMouseInput(0, 0, 0, ButtonFlags(button, isDown: true)));

    public InputInjectionResult MouseUp(MouseButton button) =>
        Send(CreateMouseInput(0, 0, 0, ButtonFlags(button, isDown: false)));

    public InputInjectionResult Scroll(int delta) =>
        Send(CreateMouseInput(0, 0, unchecked((uint)delta), MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL));

    public InputInjectionResult KeyDown(ushort keyCode) => Send(CreateKeyboardInput(keyCode, isDown: true));

    public InputInjectionResult KeyUp(ushort keyCode) => Send(CreateKeyboardInput(keyCode, isDown: false));

    private static MOUSE_EVENT_FLAGS ButtonFlags(MouseButton button, bool isDown) => (button, isDown) switch
    {
        (MouseButton.Left, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
        (MouseButton.Left, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
        (MouseButton.Right, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
        (MouseButton.Right, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
        (MouseButton.Middle, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN,
        (MouseButton.Middle, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
    };

    private static INPUT CreateMouseInput(int x, int y, uint mouseData, MOUSE_EVENT_FLAGS flags)
    {
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                mi = new MOUSEINPUT
                {
                    dx = x,
                    dy = y,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = ApplicationInputMarker.Signature
                }
            }
        };
    }

    // Windows, WM_KEYDOWN/UP mesajlarındaki "genişletilmiş tuş" (extended key) bitini bu tuşlar
    // için ayırt eder (ör. sağdaki Ctrl/Alt'ı soldakinden, ok tuşlarını numpad eşdeğerlerinden).
    // KEYEVENTF_EXTENDEDKEY bayrağı verilmezse SendInput bu tuşları YANLIŞ (numpad) taraf gibi
    // sentezler; bu da hem hedef uygulamaya hem de bu uygulamanın kendi WH_KEYBOARD_LL kancasına
    // (kayıt sırasında kendi ürettiği input'u ayırt etmesi gerektiğinde) hatalı bir tarama kodu
    // gönderilmesine yol açar.
    private static readonly HashSet<ushort> ExtendedKeyCodes =
    [
        0x21, // VK_PRIOR (Page Up)
        0x22, // VK_NEXT (Page Down)
        0x23, // VK_END
        0x24, // VK_HOME
        0x25, // VK_LEFT
        0x26, // VK_UP
        0x27, // VK_RIGHT
        0x28, // VK_DOWN
        0x2C, // VK_SNAPSHOT (Print Screen)
        0x2D, // VK_INSERT
        0x2E, // VK_DELETE
        0x6F, // VK_DIVIDE (numpad /)
        0x90, // VK_NUMLOCK
        0xA3, // VK_RCONTROL
        0xA5  // VK_RMENU (sağ Alt)
    ];

    private static INPUT CreateKeyboardInput(ushort keyCode, bool isDown)
    {
        var flags = isDown ? default(KEYBD_EVENT_FLAGS) : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        if (ExtendedKeyCodes.Contains(keyCode))
        {
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
        }

        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = (VIRTUAL_KEY)keyCode,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = ApplicationInputMarker.Signature
                }
            }
        };
    }

    private unsafe InputInjectionResult Send(INPUT input)
    {
        Span<INPUT> inputs = [input];
        uint sent;
        fixed (INPUT* pInputs = inputs)
        {
            sent = PInvoke.SendInput(1, pInputs, sizeof(INPUT));
        }

        if (sent == 1)
        {
            return new InputInjectionResult(true, 1, 1, null, null);
        }

        var nativeError = Marshal.GetLastPInvokeError();
        _logger.LogError(null, "SendInput başarısız oldu. Native hata kodu: {0}", nativeError);

        return new InputInjectionResult(
            false,
            1,
            0,
            nativeError,
            $"SendInput başarısız oldu (native hata kodu: {nativeError}). Hedef uygulama daha yüksek yetkiyle çalışıyor olabilir.");
    }
}
