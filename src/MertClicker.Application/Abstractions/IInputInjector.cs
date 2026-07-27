using MertClicker.Application.Models;
using MertClicker.Domain;
using MertClicker.Domain.Display;

namespace MertClicker.Application.Abstractions;

public interface IInputInjector
{
    InputInjectionResult MoveMouse(ScreenPoint point);

    InputInjectionResult MouseDown(MouseButton button);

    InputInjectionResult MouseUp(MouseButton button);

    InputInjectionResult Scroll(int delta);

    InputInjectionResult KeyDown(ushort keyCode);

    InputInjectionResult KeyUp(ushort keyCode);
}
