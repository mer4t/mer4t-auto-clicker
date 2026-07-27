namespace MertClicker.Application.Models;

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public required string HotkeyId { get; init; }
}
