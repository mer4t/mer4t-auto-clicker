using MertClicker.Domain.Display;

namespace MertClicker.Application.Abstractions;

public interface IDisplayService
{
    DisplaySnapshot GetSnapshot();
}
