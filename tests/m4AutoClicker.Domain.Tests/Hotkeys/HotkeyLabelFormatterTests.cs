using m4AutoClicker.Domain.Hotkeys;

namespace m4AutoClicker.Domain.Tests.Hotkeys;

public sealed class HotkeyLabelFormatterTests
{
    [Fact]
    public void Format_With_No_Modifiers_Returns_Just_The_Key()
    {
        var definition = new HotkeyDefinition { Id = "test", Key = VirtualKey.F6 };

        Assert.Equal("F6", HotkeyLabelFormatter.Format(definition));
    }

    [Fact]
    public void Format_With_Single_Modifier_Joins_With_Plus()
    {
        var definition = new HotkeyDefinition { Id = "test", Key = VirtualKey.F6, Modifiers = HotkeyModifiers.Control };

        Assert.Equal("Control+F6", HotkeyLabelFormatter.Format(definition));
    }

    [Fact]
    public void Format_With_Multiple_Modifiers_Joins_All_With_Plus_Not_Comma()
    {
        var definition = new HotkeyDefinition
        {
            Id = "test",
            Key = VirtualKey.F6,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt
        };

        var label = HotkeyLabelFormatter.Format(definition);

        Assert.Equal("Control+Alt+Shift+F6", label);
        Assert.DoesNotContain(",", label);
    }
}
