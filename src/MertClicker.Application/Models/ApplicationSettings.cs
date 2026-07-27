using MertClicker.Domain.Hotkeys;

namespace MertClicker.Application.Models;

public sealed record ApplicationSettings
{
    public MouseMovementSamplingSettings MouseMovementSampling { get; init; } = new();

    public IReadOnlyList<HotkeyDefinition> Hotkeys { get; init; } = HotkeyDefaults.All;
}
