namespace m4AutoClicker.Application.Models;

public sealed record HotkeyRegistrationResult
{
    public required bool Success { get; init; }

    public required string HotkeyId { get; init; }

    public HotkeyRegistrationErrorType ErrorType { get; init; } = HotkeyRegistrationErrorType.None;

    public int? NativeErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static HotkeyRegistrationResult Successful(string hotkeyId) =>
        new() { Success = true, HotkeyId = hotkeyId };

    public static HotkeyRegistrationResult Failed(
        string hotkeyId,
        HotkeyRegistrationErrorType errorType,
        string errorMessage,
        int? nativeErrorCode = null) =>
        new()
        {
            Success = false,
            HotkeyId = hotkeyId,
            ErrorType = errorType,
            ErrorMessage = errorMessage,
            NativeErrorCode = nativeErrorCode
        };
}
