using System.Text.Json;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Application.Tests.Fakes;

namespace m4AutoClicker.Application.Tests.Services;

public class MacroMigrationCoordinatorTests
{
    [Fact]
    public void MigrateToLatest_Returns_Same_Document_When_Already_At_Target_Version()
    {
        var coordinator = new MacroMigrationCoordinator([]);
        using var document = JsonDocument.Parse("{\"schemaVersion\":1}");

        var result = coordinator.MigrateToLatest(document, fromVersion: 1, targetVersion: 1);

        Assert.Same(document, result);
    }

    [Fact]
    public void MigrateToLatest_Applies_A_Single_Migration_Step()
    {
        var migration = new FakeMacroMigration { FromVersion = 1, ToVersion = 2 };
        var coordinator = new MacroMigrationCoordinator([migration]);
        using var document = JsonDocument.Parse("{\"schemaVersion\":1}");

        using var result = coordinator.MigrateToLatest(document, fromVersion: 1, targetVersion: 2);

        Assert.Equal(1, migration.CallCount);
        Assert.Equal(2, result.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void MigrateToLatest_Chains_Multiple_Migration_Steps_In_Order()
    {
        var migration1To2 = new FakeMacroMigration { FromVersion = 1, ToVersion = 2 };
        var migration2To3 = new FakeMacroMigration { FromVersion = 2, ToVersion = 3 };
        var coordinator = new MacroMigrationCoordinator([migration2To3, migration1To2]); // kasıtlı ters sıra
        using var document = JsonDocument.Parse("{\"schemaVersion\":1}");

        using var result = coordinator.MigrateToLatest(document, fromVersion: 1, targetVersion: 3);

        Assert.Equal(1, migration1To2.CallCount);
        Assert.Equal(1, migration2To3.CallCount);
        Assert.Equal(3, result.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void MigrateToLatest_Throws_When_A_Required_Step_Is_Missing()
    {
        var coordinator = new MacroMigrationCoordinator([]);
        using var document = JsonDocument.Parse("{\"schemaVersion\":1}");

        var exception = Record.Exception(() => coordinator.MigrateToLatest(document, fromVersion: 1, targetVersion: 2));

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void MigrateToLatest_Throws_When_From_Is_Newer_Than_Target()
    {
        var coordinator = new MacroMigrationCoordinator([]);
        using var document = JsonDocument.Parse("{\"schemaVersion\":2}");

        var exception = Record.Exception(() => coordinator.MigrateToLatest(document, fromVersion: 2, targetVersion: 1));

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void MigrateToLatest_Throws_When_Migration_Step_Does_Not_Advance_Version()
    {
        var stuckMigration = new FakeMacroMigration { FromVersion = 1, ToVersion = 1 };
        var coordinator = new MacroMigrationCoordinator([stuckMigration]);
        using var document = JsonDocument.Parse("{\"schemaVersion\":1}");

        var exception = Record.Exception(() => coordinator.MigrateToLatest(document, fromVersion: 1, targetVersion: 2));

        Assert.IsType<InvalidOperationException>(exception);
    }
}
