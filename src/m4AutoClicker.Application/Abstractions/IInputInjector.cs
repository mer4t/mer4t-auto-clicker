using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Abstractions;

public interface IInputInjector
{
    InputInjectionResult MoveMouse(ScreenPoint point);

    InputInjectionResult MouseDown(MouseButton button);

    InputInjectionResult MouseUp(MouseButton button);

    InputInjectionResult Scroll(int delta);

    InputInjectionResult KeyDown(ushort keyCode);

    InputInjectionResult KeyUp(ushort keyCode);
}
