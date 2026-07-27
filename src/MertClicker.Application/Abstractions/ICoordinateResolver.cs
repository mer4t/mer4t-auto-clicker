using MertClicker.Application.Models;
using MertClicker.Domain.Display;

namespace MertClicker.Application.Abstractions;

public interface ICoordinateResolver
{
    CoordinateResolutionResult Resolve(CoordinateTarget target, DisplaySnapshot currentDisplays);
}
