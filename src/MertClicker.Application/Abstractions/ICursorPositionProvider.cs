using MertClicker.Domain.Display;

namespace MertClicker.Application.Abstractions;

public interface ICursorPositionProvider
{
    ScreenPoint GetCurrentPosition();
}
