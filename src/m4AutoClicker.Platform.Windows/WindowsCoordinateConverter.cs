using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Platform.Windows;

public sealed class WindowsCoordinateConverter
{
    private readonly IDisplayService _displayService;

    public WindowsCoordinateConverter(IDisplayService displayService)
    {
        _displayService = displayService;
    }

    public (int X, int Y) ToVirtualDesktopNormalized(ScreenPoint point)
    {
        var snapshot = _displayService.GetSnapshot();

        return (
            NormalizeAxis(point.X, snapshot.VirtualLeft, snapshot.VirtualWidth),
            NormalizeAxis(point.Y, snapshot.VirtualTop, snapshot.VirtualHeight));
    }

    private static int NormalizeAxis(int value, int origin, int extent)
    {
        if (extent <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(extent), extent, "Sanal masaüstü genişliği veya yüksekliği sıfır ya da negatif olamaz.");
        }

        var normalized = (long)(value - origin) * 65536L / extent;
        return (int)Math.Clamp(normalized, 0, 65535);
    }
}
