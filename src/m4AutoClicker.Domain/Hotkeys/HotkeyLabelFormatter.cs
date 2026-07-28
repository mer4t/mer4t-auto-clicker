namespace m4AutoClicker.Domain.Hotkeys;

public static class HotkeyLabelFormatter
{
    public static string Format(HotkeyDefinition definition)
    {
        if (definition.Modifiers == HotkeyModifiers.None)
        {
            return definition.Key.ToString();
        }

        // HotkeyModifiers bir [Flags] enum'dur; birden fazla bit set olduğunda varsayılan
        // Enum.ToString() "Alt, Control" gibi virgülle ayrılmış bir liste üretir, "+" ile
        // birleştirmez. Bu yüzden bitler burada tek tek "+" ile birleştirilir.
        var parts = new List<string>(4);
        if (definition.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add(nameof(HotkeyModifiers.Control));
        if (definition.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add(nameof(HotkeyModifiers.Alt));
        if (definition.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add(nameof(HotkeyModifiers.Shift));
        if (definition.Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add(nameof(HotkeyModifiers.Windows));
        parts.Add(definition.Key.ToString());

        return string.Join("+", parts);
    }
}
