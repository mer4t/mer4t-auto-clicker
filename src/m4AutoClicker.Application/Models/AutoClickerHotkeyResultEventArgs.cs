namespace m4AutoClicker.Application.Models;

public sealed class AutoClickerHotkeyResultEventArgs : EventArgs
{
    public required bool IsRunning { get; init; }

    public required string StatusMessage { get; init; }
}
