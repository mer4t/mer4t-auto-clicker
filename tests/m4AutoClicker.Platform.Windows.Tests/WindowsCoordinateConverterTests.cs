using m4AutoClicker.Domain.Display;
using m4AutoClicker.Platform.Windows.Tests.Fakes;

namespace m4AutoClicker.Platform.Windows.Tests;

public class WindowsCoordinateConverterTests
{
    private static WindowsCoordinateConverter CreateConverter(DisplaySnapshot snapshot) =>
        new(new FakeDisplayService { Snapshot = snapshot });

    [Fact]
    public void Origin_Maps_To_Zero_On_Single_Monitor()
    {
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (x, y) = converter.ToVirtualDesktopNormalized(new ScreenPoint(0, 0));

        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void FarCorner_Maps_Close_To_Max_On_Single_Monitor()
    {
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (x, y) = converter.ToVirtualDesktopNormalized(new ScreenPoint(1919, 1079));

        Assert.InRange(x, 65000, 65535);
        Assert.InRange(y, 65000, 65535);
    }

    [Fact]
    public void Midpoint_Maps_Close_To_Half_Range()
    {
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (x, _) = converter.ToVirtualDesktopNormalized(new ScreenPoint(960, 540));

        Assert.InRange(x, 32000, 33500);
    }

    [Fact]
    public void Second_Monitor_To_The_Right_Uses_Combined_Virtual_Width()
    {
        // İki adet 1920x1080 monitör yan yana: sanal masaüstü 3840x1080.
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 3840,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (leftMonitorPoint, _) = converter.ToVirtualDesktopNormalized(new ScreenPoint(960, 540));
        var (rightMonitorPoint, _) = converter.ToVirtualDesktopNormalized(new ScreenPoint(2880, 540));

        Assert.True(rightMonitorPoint > leftMonitorPoint);
        Assert.InRange(leftMonitorPoint, 16000, 16500);
        Assert.InRange(rightMonitorPoint, 49000, 49500);
    }

    [Fact]
    public void Monitor_With_Negative_Origin_On_The_Left_Normalizes_Correctly()
    {
        // Ana monitör solda negatif koordinatlarda, ikincil monitör sağda (0,0)'da başlıyor.
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = -1920,
            VirtualTop = 0,
            VirtualWidth = 3840,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (originX, _) = converter.ToVirtualDesktopNormalized(new ScreenPoint(-1920, 0));
        var (secondMonitorOriginX, _) = converter.ToVirtualDesktopNormalized(new ScreenPoint(0, 0));

        Assert.Equal(0, originX);
        Assert.InRange(secondMonitorOriginX, 32500, 33000);
    }

    [Fact]
    public void NonZero_Virtual_Origin_Is_Subtracted_Before_Normalizing()
    {
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 500,
            VirtualTop = 200,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (x, y) = converter.ToVirtualDesktopNormalized(new ScreenPoint(500, 200));

        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void Point_Outside_Bounds_Is_Clamped_Instead_Of_Throwing()
    {
        var converter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors = []
        });

        var (x, y) = converter.ToVirtualDesktopNormalized(new ScreenPoint(5000, -100));

        Assert.Equal(65535, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void Monitor_Dpi_Does_Not_Affect_Pixel_Normalization()
    {
        // SendInput her zaman fiziksel piksel bekler; PerMonitorV2 farkındalığı sayesinde
        // DPI ölçeklendirmesi normalize hesaplamasını etkilememelidir.
        var snapshot = new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1920,
            VirtualHeight = 1080,
            Monitors =
            [
                new MonitorSnapshot
                {
                    DeviceId = "\\\\.\\DISPLAY1",
                    Bounds = new MonitorBounds(0, 0, 1920, 1080),
                    WorkingArea = new MonitorBounds(0, 0, 1920, 1040),
                    DpiX = 192,
                    DpiY = 192,
                    IsPrimary = true
                }
            ]
        };

        var converter = CreateConverter(snapshot);

        var (x, y) = converter.ToVirtualDesktopNormalized(new ScreenPoint(960, 540));

        Assert.InRange(x, 32000, 33500);
        Assert.InRange(y, 32000, 33500);
    }

    [Fact]
    public void Different_Resolutions_Produce_Different_Normalization_Scale()
    {
        var lowResConverter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 1280,
            VirtualHeight = 720,
            Monitors = []
        });

        var highResConverter = CreateConverter(new DisplaySnapshot
        {
            VirtualLeft = 0,
            VirtualTop = 0,
            VirtualWidth = 3840,
            VirtualHeight = 2160,
            Monitors = []
        });

        var (lowResX, _) = lowResConverter.ToVirtualDesktopNormalized(new ScreenPoint(640, 360));
        var (highResX, _) = highResConverter.ToVirtualDesktopNormalized(new ScreenPoint(640, 360));

        Assert.True(highResX < lowResX);
    }
}
