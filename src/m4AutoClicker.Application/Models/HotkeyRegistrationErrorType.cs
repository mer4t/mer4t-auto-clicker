namespace m4AutoClicker.Application.Models;

public enum HotkeyRegistrationErrorType
{
    None,
    WindowHandleUnavailable,
    AlreadyRegistered,
    CombinationAlreadyUsed,
    RegistrationRejected,
    InvalidDefinition,
    Unknown
}
