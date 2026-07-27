using MertClicker.Domain.Display;

namespace MertClicker.Domain.Tests.Display;

public class MonitorBoundsTests
{
    [Fact]
    public void Width_And_Height_Are_Computed_From_Edges()
    {
        var bounds = new MonitorBounds(Left: -1920, Top: 0, Right: 0, Bottom: 1080);

        Assert.Equal(1920, bounds.Width);
        Assert.Equal(1080, bounds.Height);
    }
}
