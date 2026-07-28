namespace m4AutoClicker.Domain.Hotkeys;

public sealed record HotkeyDefinition
{
    public required string Id { get; init; }

    public required VirtualKey Key { get; init; }

    public HotkeyModifiers Modifiers { get; init; } = HotkeyModifiers.None;
}
