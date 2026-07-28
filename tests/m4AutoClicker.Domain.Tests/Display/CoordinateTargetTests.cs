using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Domain.Tests.Display;

public class CoordinateTargetTests
{
    [Fact]
    public void CurrentCursor_Returns_CurrentCursorTarget()
    {
        Assert.IsType<CurrentCursorTarget>(CoordinateTarget.CurrentCursor);
    }

    [Fact]
    public void FixedPoint_Returns_FixedPointTarget_With_Given_Point()
    {
        var point = new ScreenPoint(120, -40);

        var target = CoordinateTarget.FixedPoint(point);

        var fixedTarget = Assert.IsType<FixedPointTarget>(target);
        Assert.Equal(point, fixedTarget.Point);
    }
}
