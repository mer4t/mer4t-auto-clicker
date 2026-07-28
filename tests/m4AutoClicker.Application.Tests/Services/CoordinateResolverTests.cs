using m4AutoClicker.Application.Services;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Tests.Services;

public class CoordinateResolverTests
{
    private readonly CoordinateResolver _resolver = new();

    private static DisplaySnapshot SingleMonitor() => new()
    {
        VirtualLeft = 0,
        VirtualTop = 0,
        VirtualWidth = 1920,
        VirtualHeight = 1080,
        Monitors = []
    };

    private static DisplaySnapshot SecondMonitorOnRight() => new()
    {
        VirtualLeft = 0,
        VirtualTop = 0,
        VirtualWidth = 3840,
        VirtualHeight = 1080,
        Monitors = []
    };

    private static DisplaySnapshot NegativeMonitorOnLeft() => new()
    {
        VirtualLeft = -1920,
        VirtualTop = 0,
        VirtualWidth = 3840,
        VirtualHeight = 1080,
        Monitors = []
    };

    private static DisplaySnapshot NonZeroVirtualOrigin() => new()
    {
        VirtualLeft = 500,
        VirtualTop = 200,
        VirtualWidth = 1920,
        VirtualHeight = 1080,
        Monitors = []
    };

    [Fact]
    public void CurrentCursor_Resolves_To_NoMoveRequired()
    {
        var result = _resolver.Resolve(CoordinateTarget.CurrentCursor, SingleMonitor());

        Assert.True(result.Success);
        Assert.Null(result.Point);
    }

    [Fact]
    public void FixedPoint_Within_Single_Monitor_Bounds_Resolves_Successfully()
    {
        var target = CoordinateTarget.FixedPoint(new ScreenPoint(960, 540));

        var result = _resolver.Resolve(target, SingleMonitor());

        Assert.True(result.Success);
        Assert.Equal(new ScreenPoint(960, 540), result.Point);
    }

    [Theory]
    [InlineData(-1, 540)]
    [InlineData(1920, 540)]
    [InlineData(960, -1)]
    [InlineData(960, 1080)]
    public void FixedPoint_Outside_Single_Monitor_Bounds_Fails(int x, int y)
    {
        var target = CoordinateTarget.FixedPoint(new ScreenPoint(x, y));

        var result = _resolver.Resolve(target, SingleMonitor());

        Assert.False(result.Success);
        Assert.Null(result.Point);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void FixedPoint_On_Second_Monitor_To_The_Right_Resolves_Successfully()
    {
        var target = CoordinateTarget.FixedPoint(new ScreenPoint(2500, 300));

        var result = _resolver.Resolve(target, SecondMonitorOnRight());

        Assert.True(result.Success);
        Assert.Equal(new ScreenPoint(2500, 300), result.Point);
    }

    [Fact]
    public void FixedPoint_With_Negative_Coordinates_On_Left_Monitor_Resolves_Successfully()
    {
        var target = CoordinateTarget.FixedPoint(new ScreenPoint(-960, 540));

        var result = _resolver.Resolve(target, NegativeMonitorOnLeft());

        Assert.True(result.Success);
        Assert.Equal(new ScreenPoint(-960, 540), result.Point);
    }

    [Fact]
    public void FixedPoint_Before_Negative_Virtual_Left_Fails()
    {
        var target = CoordinateTarget.FixedPoint(new ScreenPoint(-1921, 540));

        var result = _resolver.Resolve(target, NegativeMonitorOnLeft());

        Assert.False(result.Success);
    }

    [Fact]
    public void FixedPoint_Respects_NonZero_Virtual_Origin()
    {
        var withinBounds = CoordinateTarget.FixedPoint(new ScreenPoint(600, 300));
        var beforeOrigin = CoordinateTarget.FixedPoint(new ScreenPoint(400, 300));

        var snapshot = NonZeroVirtualOrigin();

        Assert.True(_resolver.Resolve(withinBounds, snapshot).Success);
        Assert.False(_resolver.Resolve(beforeOrigin, snapshot).Success);
    }
}
