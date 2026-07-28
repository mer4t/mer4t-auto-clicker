using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Domain.Tests.Macros;

public class MacroTests
{
    [Fact]
    public void Macro_With_Expression_Creates_Independent_Copy_With_Updated_Field()
    {
        var original = new Macro
        {
            Id = Guid.NewGuid(),
            Name = "Örnek Makro",
            SchemaVersion = 1,
            CreatedAtUtc = DateTime.UnixEpoch,
            UpdatedAtUtc = DateTime.UnixEpoch,
            DurationTicks = 0,
            DisplaySnapshot = new DisplaySnapshot
            {
                VirtualLeft = 0,
                VirtualTop = 0,
                VirtualWidth = 1920,
                VirtualHeight = 1080,
                Monitors = []
            },
            Actions =
            [
                new MouseMoveAction { OffsetTicks = 0, X = 100, Y = 200 },
                new MouseButtonDownAction { OffsetTicks = 100, Button = MouseButton.Left },
                new MouseButtonUpAction { OffsetTicks = 200, Button = MouseButton.Left }
            ]
        };

        var renamed = original with { Name = "Yeni İsim" };

        Assert.Equal("Örnek Makro", original.Name);
        Assert.Equal("Yeni İsim", renamed.Name);
        Assert.Equal(3, renamed.Actions.Count);
        Assert.Same(original.Actions, renamed.Actions);
    }
}
