using MertClicker.Application.Services;
using MertClicker.Domain;
using MertClicker.Domain.Display;
using MertClicker.Domain.Macros;

namespace MertClicker.Application.Tests.Services;

public class MacroValidatorTests
{
    private static Macro CreateMacro(string name = "Test Makro", int schemaVersion = 1, IReadOnlyList<MacroAction>? actions = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        SchemaVersion = schemaVersion,
        CreatedAtUtc = DateTime.UnixEpoch,
        UpdatedAtUtc = DateTime.UnixEpoch,
        DurationTicks = 100,
        DisplaySnapshot = new DisplaySnapshot { VirtualLeft = 0, VirtualTop = 0, VirtualWidth = 1920, VirtualHeight = 1080, Monitors = [] },
        Actions = actions ?? [new MouseButtonDownAction { OffsetTicks = 0, Button = MouseButton.Left }]
    };

    [Fact]
    public void Validate_Returns_Valid_For_Well_Formed_Macro()
    {
        var validator = new MacroValidator();

        var result = validator.Validate(CreateMacro());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Rejects_Empty_Or_Whitespace_Name(string name)
    {
        var validator = new MacroValidator();

        var result = validator.Validate(CreateMacro(name: name));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("adı"));
    }

    [Fact]
    public void Validate_Rejects_Macro_With_No_Actions()
    {
        var validator = new MacroValidator();

        var result = validator.Validate(CreateMacro(actions: []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("eylem"));
    }

    [Fact]
    public void Validate_Rejects_Non_Positive_Schema_Version()
    {
        var validator = new MacroValidator();

        var result = validator.Validate(CreateMacro(schemaVersion: 0));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("şema"));
    }

    [Fact]
    public void Validate_Rejects_Negative_Action_Offset()
    {
        var validator = new MacroValidator();
        var macro = CreateMacro(actions: [new MouseButtonDownAction { OffsetTicks = -1, Button = MouseButton.Left }]);

        var result = validator.Validate(macro);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("zaman ofseti"));
    }

    [Fact]
    public void Validate_Rejects_Negative_Delay_Duration()
    {
        var validator = new MacroValidator();
        var macro = CreateMacro(actions: [new DelayAction { OffsetTicks = 0, DurationTicks = -5 }]);

        var result = validator.Validate(macro);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("bekleme süresi"));
    }

    [Fact]
    public void Validate_Collects_Multiple_Errors_At_Once()
    {
        var validator = new MacroValidator();
        var macro = CreateMacro(name: "", schemaVersion: 0, actions: []);

        var result = validator.Validate(macro);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }
}
