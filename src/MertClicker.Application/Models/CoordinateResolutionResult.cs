using MertClicker.Domain.Display;

namespace MertClicker.Application.Models;

public sealed record CoordinateResolutionResult
{
    public required bool Success { get; init; }

    public ScreenPoint? Point { get; init; }

    public string? ErrorMessage { get; init; }

    public static CoordinateResolutionResult NoMoveRequired { get; } = new() { Success = true, Point = null };

    public static CoordinateResolutionResult Resolved(ScreenPoint point) =>
        new() { Success = true, Point = point };

    public static CoordinateResolutionResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
