using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Models;

public sealed class MacroRecorderHotkeyResultEventArgs : EventArgs
{
    public required bool IsRecording { get; init; }

    public required string StatusMessage { get; init; }

    public Macro? RecordedMacro { get; init; }
}
