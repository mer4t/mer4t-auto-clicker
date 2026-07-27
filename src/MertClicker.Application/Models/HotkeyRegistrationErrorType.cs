namespace MertClicker.Application.Models;

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
