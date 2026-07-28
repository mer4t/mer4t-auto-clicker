namespace m4AutoClicker.Application.Models;

public sealed class HotkeyPressedEventArgs : EventArgs
{
    public required string HotkeyId { get; init; }
}
