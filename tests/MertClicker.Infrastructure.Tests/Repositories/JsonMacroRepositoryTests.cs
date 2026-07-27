using System.Text.Json;
using MertClicker.Application.Services;
using MertClicker.Domain;
using MertClicker.Domain.Display;
using MertClicker.Domain.Macros;
using MertClicker.Infrastructure.Repositories;
using MertClicker.Infrastructure.Tests.Fakes;

namespace MertClicker.Infrastructure.Tests.Repositories;

public sealed class JsonMacroRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "MertClickerTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private JsonMacroRepository CreateRepository(out FakeApplicationLogger logger)
    {
        var paths = new ApplicationPaths(_tempRoot);
        var migrationCoordinator = new MacroMigrationCoordinator([]);
        logger = new FakeApplicationLogger();
        return new JsonMacroRepository(paths, migrationCoordinator, logger);
    }

    private static Macro CreateMacro(string name = "Test Makro") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Açıklama",
        SchemaVersion = 1,
        CreatedAtUtc = DateTime.UnixEpoch,
        UpdatedAtUtc = DateTime.UnixEpoch,
        DurationTicks = 1000,
        DisplaySnapshot = new DisplaySnapshot { VirtualLeft = 0, VirtualTop = 0, VirtualWidth = 1920, VirtualHeight = 1080, Monitors = [] },
        Tags = ["test", "örnek"],
        Actions =
        [
            new MouseMoveAction { OffsetTicks = 0, X = 10, Y = 20 },
            new MouseButtonDownAction { OffsetTicks = 10, Button = MouseButton.Left },
            new MouseButtonUpAction { OffsetTicks = 20, Button = MouseButton.Left },
            new MouseWheelAction { OffsetTicks = 30, Delta = 120 },
            new DelayAction { OffsetTicks = 40, DurationTicks = 500 },
            new KeyDownAction { OffsetTicks = 50, KeyCode = 0x41 },
            new KeyUpAction { OffsetTicks = 60, KeyCode = 0x41 }
        ]
    };

    [Fact]
    public async Task SaveAsync_Then_GetByIdAsync_Round_Trips_All_Action_Types()
    {
        var repository = CreateRepository(out _);
        var macro = CreateMacro();

        await repository.SaveAsync(macro);
        var loaded = await repository.GetByIdAsync(macro.Id);

        Assert.NotNull(loaded);
        Assert.Equal(macro.Id, loaded!.Id);
        Assert.Equal(macro.Name, loaded.Name);
        Assert.Equal(macro.Description, loaded.Description);
        Assert.Equal(macro.Tags, loaded.Tags);
        Assert.Equal(7, loaded.Actions.Count);
        Assert.IsType<MouseMoveAction>(loaded.Actions[0]);
        Assert.IsType<MouseButtonDownAction>(loaded.Actions[1]);
        Assert.IsType<MouseButtonUpAction>(loaded.Actions[2]);
        Assert.IsType<MouseWheelAction>(loaded.Actions[3]);
        Assert.IsType<DelayAction>(loaded.Actions[4]);
        Assert.IsType<KeyDownAction>(loaded.Actions[5]);
        Assert.IsType<KeyUpAction>(loaded.Actions[6]);

        var move = (MouseMoveAction)loaded.Actions[0];
        Assert.Equal(10, move.X);
        Assert.Equal(20, move.Y);

        var wheel = (MouseWheelAction)loaded.Actions[3];
        Assert.Equal(120, wheel.Delta);

        var delay = (DelayAction)loaded.Actions[4];
        Assert.Equal(500, delay.DurationTicks);

        var keyDown = (KeyDownAction)loaded.Actions[5];
        Assert.Equal((ushort)0x41, keyDown.KeyCode);

        var keyUp = (KeyUpAction)loaded.Actions[6];
        Assert.Equal((ushort)0x41, keyUp.KeyCode);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_For_Unknown_Id()
    {
        var repository = CreateRepository(out _);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Summaries_For_All_Saved_Macros()
    {
        var repository = CreateRepository(out _);
        var older = CreateMacro("Eski") with { UpdatedAtUtc = DateTime.UnixEpoch };
        var newer = CreateMacro("Yeni") with { UpdatedAtUtc = DateTime.UnixEpoch.AddDays(1) };

        await repository.SaveAsync(older);
        await repository.SaveAsync(newer);

        var summaries = await repository.GetAllAsync();

        Assert.Equal(2, summaries.Count);
        Assert.Equal("Yeni", summaries[0].Name); // en güncel önce
        Assert.Equal("Eski", summaries[1].Name);
        Assert.Equal(7, summaries[0].ActionCount);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Empty_List_When_No_Macros_Saved()
    {
        var repository = CreateRepository(out _);

        var summaries = await repository.GetAllAsync();

        Assert.Empty(summaries);
    }

    [Fact]
    public async Task DeleteAsync_Removes_The_Macro()
    {
        var repository = CreateRepository(out _);
        var macro = CreateMacro();
        await repository.SaveAsync(macro);

        await repository.DeleteAsync(macro.Id);
        var result = await repository.GetByIdAsync(macro.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_Is_Safe_For_Unknown_Id()
    {
        var repository = CreateRepository(out _);

        var exception = await Record.ExceptionAsync(() => repository.DeleteAsync(Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAllAsync_Skips_Corrupted_Files_And_Logs_Error()
    {
        var repository = CreateRepository(out var logger);
        var macro = CreateMacro();
        await repository.SaveAsync(macro);

        var paths = new ApplicationPaths(_tempRoot);
        var corruptedFilePath = Path.Combine(paths.MacrosDirectory, "corrupted.json");
        await File.WriteAllTextAsync(corruptedFilePath, "{ bu geçerli bir json değil");

        var summaries = await repository.GetAllAsync();

        Assert.Single(summaries);
        Assert.Equal(macro.Id, summaries[0].Id);
        Assert.NotEmpty(logger.ErrorMessages);
    }

    [Fact]
    public async Task ExportAsync_Writes_Macro_Json_To_The_Given_File_Path()
    {
        var repository = CreateRepository(out _);
        var macro = CreateMacro();
        await repository.SaveAsync(macro);

        var destinationPath = Path.Combine(_tempRoot, "export.json");
        await repository.ExportAsync(macro.Id, destinationPath);

        Assert.True(File.Exists(destinationPath));
        var exported = JsonSerializer.Deserialize<Macro>(
            await File.ReadAllTextAsync(destinationPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(exported);
        Assert.Equal(macro.Id, exported!.Id);
        Assert.Equal(macro.Name, exported.Name);
        Assert.Equal(7, exported.Actions.Count);
    }

    [Fact]
    public async Task ExportAsync_Throws_For_Unknown_Id()
    {
        var repository = CreateRepository(out _);
        var destinationPath = Path.Combine(_tempRoot, "export.json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExportAsync(Guid.NewGuid(), destinationPath));
    }

    [Fact]
    public async Task ImportAsync_Reads_A_Previously_Exported_File_And_Assigns_A_New_Id()
    {
        var repository = CreateRepository(out _);
        var macro = CreateMacro();
        await repository.SaveAsync(macro);

        var exportedPath = Path.Combine(_tempRoot, "export.json");
        await repository.ExportAsync(macro.Id, exportedPath);

        var imported = await repository.ImportAsync(exportedPath);

        Assert.NotEqual(macro.Id, imported.Id);
        Assert.Equal(macro.Name, imported.Name);
        Assert.Equal(7, imported.Actions.Count);

        var all = await repository.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ImportAsync_Throws_For_A_File_That_Is_Not_A_Valid_Macro()
    {
        var repository = CreateRepository(out _);
        var invalidPath = Path.Combine(_tempRoot, "invalid.json");
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(invalidPath, "{ bu geçerli bir makro json'u değil");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ImportAsync(invalidPath));
    }

    [Fact]
    public async Task SaveAsync_Overwrites_Existing_Macro_With_Same_Id()
    {
        var repository = CreateRepository(out _);
        var macro = CreateMacro("İlk isim");
        await repository.SaveAsync(macro);

        var renamed = macro with { Name = "Güncellenmiş isim" };
        await repository.SaveAsync(renamed);

        var loaded = await repository.GetByIdAsync(macro.Id);

        Assert.Equal("Güncellenmiş isim", loaded!.Name);
        var all = await repository.GetAllAsync();
        Assert.Single(all);
    }

    [Fact]
    public async Task SaveAsync_Handles_Many_Concurrent_Calls_For_The_Same_Macro_Without_Throwing()
    {
        // Aynı temp dosya yoluna yazan eşzamanlı SaveAsync çağrıları, bir kilit olmadan birbirinin
        // dosyasını yarıda kesip File.Move'u başarısız kılabilir.
        var repository = CreateRepository(out _);
        var macro = CreateMacro();

        var tasks = Enumerable.Range(0, 20)
            .Select(i => repository.SaveAsync(macro with { Name = $"İsim {i}" }))
            .ToArray();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));

        Assert.Null(exception);
        var loaded = await repository.GetByIdAsync(macro.Id);
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task ExportAsync_Creates_Missing_Destination_Directory()
    {
        var repository = CreateRepository(out _);
        var macro = CreateMacro();
        await repository.SaveAsync(macro);

        var destinationPath = Path.Combine(_tempRoot, "alt-klasor", "export.json");
        await repository.ExportAsync(macro.Id, destinationPath);

        Assert.True(File.Exists(destinationPath));
    }
}
