namespace m4AutoClicker.Application.Models;

public sealed class MacroPlaybackHotkeyResultEventArgs : EventArgs
{
    public required bool IsPlaying { get; init; }

    public required string StatusMessage { get; init; }
}
