using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Abstractions;

public interface ICoordinateResolver
{
    CoordinateResolutionResult Resolve(CoordinateTarget target, DisplaySnapshot currentDisplays);
}
