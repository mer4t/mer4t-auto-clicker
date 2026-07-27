namespace MertClicker.Application.Models;

public sealed record MacroValidationResult
{
    public required bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static MacroValidationResult Valid() => new() { IsValid = true };

    public static MacroValidationResult Invalid(params string[] errors) =>
        new() { IsValid = false, Errors = errors };
}
