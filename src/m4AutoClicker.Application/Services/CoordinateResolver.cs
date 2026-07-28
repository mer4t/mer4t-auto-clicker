using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Services;

public sealed class CoordinateResolver : ICoordinateResolver
{
    public CoordinateResolutionResult Resolve(CoordinateTarget target, DisplaySnapshot currentDisplays)
    {
        return target switch
        {
            CurrentCursorTarget => CoordinateResolutionResult.NoMoveRequired,
            FixedPointTarget fixedPoint => ResolveFixedPoint(fixedPoint.Point, currentDisplays),
            _ => CoordinateResolutionResult.Failed($"Bilinmeyen koordinat hedefi türü: {target.GetType().Name}")
        };
    }

    private static CoordinateResolutionResult ResolveFixedPoint(ScreenPoint point, DisplaySnapshot displays)
    {
        var virtualRight = displays.VirtualLeft + displays.VirtualWidth;
        var virtualBottom = displays.VirtualTop + displays.VirtualHeight;

        if (point.X < displays.VirtualLeft || point.X >= virtualRight ||
            point.Y < displays.VirtualTop || point.Y >= virtualBottom)
        {
            return CoordinateResolutionResult.Failed(
                $"Nokta ({point.X}, {point.Y}) sanal masaüstü sınırlarının " +
                $"(({displays.VirtualLeft}, {displays.VirtualTop}) - ({virtualRight}, {virtualBottom})) dışında.");
        }

        return CoordinateResolutionResult.Resolved(point);
    }
}
