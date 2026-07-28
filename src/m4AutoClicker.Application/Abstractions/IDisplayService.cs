using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Abstractions;

public interface IDisplayService
{
    DisplaySnapshot GetSnapshot();
}
