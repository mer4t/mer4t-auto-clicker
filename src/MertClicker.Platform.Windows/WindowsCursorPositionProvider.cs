using System.Drawing;
using MertClicker.Application.Abstractions;
using MertClicker.Domain.Display;
using Windows.Win32;

namespace MertClicker.Platform.Windows;

public sealed class WindowsCursorPositionProvider : ICursorPositionProvider
{
    public ScreenPoint GetCurrentPosition()
    {
        PInvoke.GetCursorPos(out Point point);
        return new ScreenPoint(point.X, point.Y);
    }
}
