using System.Diagnostics;
using m4AutoClicker.Application.Abstractions;

namespace m4AutoClicker.Platform.Windows;

public sealed class WindowsHighResolutionClock : IHighResolutionClock
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public long Frequency => Stopwatch.Frequency;
}
