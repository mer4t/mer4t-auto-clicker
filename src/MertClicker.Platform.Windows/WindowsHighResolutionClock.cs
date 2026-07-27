using System.Diagnostics;
using MertClicker.Application.Abstractions;

namespace MertClicker.Platform.Windows;

public sealed class WindowsHighResolutionClock : IHighResolutionClock
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public long Frequency => Stopwatch.Frequency;
}
