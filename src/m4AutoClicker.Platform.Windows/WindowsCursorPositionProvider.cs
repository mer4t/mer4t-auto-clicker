using System.Drawing;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Domain.Display;
using Windows.Win32;

namespace m4AutoClicker.Platform.Windows;

public sealed class WindowsCursorPositionProvider : ICursorPositionProvider
{
    public ScreenPoint GetCurrentPosition()
    {
        PInvoke.GetCursorPos(out Point point);
        return new ScreenPoint(point.X, point.Y);
    }
}
