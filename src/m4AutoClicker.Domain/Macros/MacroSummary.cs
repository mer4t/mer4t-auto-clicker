namespace m4AutoClicker.Domain.Macros;

public sealed record MacroSummary
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }

    public required int ActionCount { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
