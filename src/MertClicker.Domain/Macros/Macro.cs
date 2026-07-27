using MertClicker.Domain.Display;

namespace MertClicker.Domain.Macros;

public sealed record Macro
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required int SchemaVersion { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }

    public required long DurationTicks { get; init; }

    public required DisplaySnapshot DisplaySnapshot { get; init; }

    public required IReadOnlyList<MacroAction> Actions { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
