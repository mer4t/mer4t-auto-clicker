using m4AutoClicker.Domain.Display;

namespace m4AutoClicker.Application.Abstractions;

public interface ICursorPositionProvider
{
    ScreenPoint GetCurrentPosition();
}
