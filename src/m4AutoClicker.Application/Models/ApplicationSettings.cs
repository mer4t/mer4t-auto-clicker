using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.Application.Models;

public sealed record ApplicationSettings
{
    public MouseMovementSamplingSettings MouseMovementSampling { get; init; } = new();

    public IReadOnlyList<HotkeyDefinition> Hotkeys { get; init; } = HotkeyDefaults.All;
}
