using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;
using MertClicker.Domain;
using MertClicker.Domain.Display;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeInputInjector : IInputInjector
{
    public List<ScreenPoint> MovedTo { get; } = [];

    public List<MouseButton> PressedButtons { get; } = [];

    public List<MouseButton> ReleasedButtons { get; } = [];

    public List<int> ScrolledDeltas { get; } = [];

    public List<ushort> PressedKeys { get; } = [];

    public List<ushort> ReleasedKeys { get; } = [];

    public InputInjectionResult MoveMouse(ScreenPoint point)
    {
        MovedTo.Add(point);
        return new InputInjectionResult(true, 1, 1, null, null);
    }

    public InputInjectionResult MouseDown(MouseButton button)
    {
        PressedButtons.Add(button);
        return new InputInjectionResult(true, 1, 1, null, null);
    }

    public InputInjectionResult MouseUp(MouseButton button)
    {
        ReleasedButtons.Add(button);
        return new InputInjectionResult(true, 1, 1, null, null);
    }

    public InputInjectionResult Scroll(int delta)
    {
        ScrolledDeltas.Add(delta);
        return new InputInjectionResult(true, 1, 1, null, null);
    }

    public InputInjectionResult KeyDown(ushort keyCode)
    {
        PressedKeys.Add(keyCode);
        return new InputInjectionResult(true, 1, 1, null, null);
    }

    public InputInjectionResult KeyUp(ushort keyCode)
    {
        ReleasedKeys.Add(keyCode);
        return new InputInjectionResult(true, 1, 1, null, null);
    }
}
