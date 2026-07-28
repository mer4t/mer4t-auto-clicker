namespace m4AutoClicker.Application.Models;

public sealed record InputInjectionResult(
    bool Success,
    int RequestedEventCount,
    int InjectedEventCount,
    int? NativeErrorCode,
    string? ErrorMessage);
