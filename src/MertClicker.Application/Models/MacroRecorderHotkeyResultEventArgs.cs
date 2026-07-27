using MertClicker.Domain.Macros;

namespace MertClicker.Application.Models;

public sealed class MacroRecorderHotkeyResultEventArgs : EventArgs
{
    public required bool IsRecording { get; init; }

    public required string StatusMessage { get; init; }

    public Macro? RecordedMacro { get; init; }
}
