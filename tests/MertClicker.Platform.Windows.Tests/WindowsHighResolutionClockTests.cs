using MertClicker.Platform.Windows;

namespace MertClicker.Platform.Windows.Tests;

public class WindowsHighResolutionClockTests
{
    [Fact]
    public void Frequency_Is_Positive()
    {
        var clock = new WindowsHighResolutionClock();

        Assert.True(clock.Frequency > 0);
    }

    [Fact]
    public void GetTimestamp_Does_Not_Go_Backwards()
    {
        var clock = new WindowsHighResolutionClock();

        var first = clock.GetTimestamp();
        var second = clock.GetTimestamp();

        Assert.True(second >= first);
    }
}
