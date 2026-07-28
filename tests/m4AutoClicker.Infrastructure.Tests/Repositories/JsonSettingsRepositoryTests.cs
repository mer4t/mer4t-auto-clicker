using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Hotkeys;
using m4AutoClicker.Infrastructure.Repositories;
using m4AutoClicker.Infrastructure.Tests.Fakes;

namespace m4AutoClicker.Infrastructure.Tests.Repositories;

public sealed class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "m4AutoClickerTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private JsonSettingsRepository CreateRepository(out FakeApplicationLogger logger)
    {
        var paths = new ApplicationPaths(_tempRoot);
        logger = new FakeApplicationLogger();
        return new JsonSettingsRepository(paths, logger);
    }

    [Fact]
    public async Task LoadAsync_Returns_Defaults_When_File_Does_Not_Exist()
    {
        var repository = CreateRepository(out _);

        var settings = await repository.LoadAsync();

        Assert.Equal(8, settings.MouseMovementSampling.MinimumIntervalMilliseconds);
        Assert.Equal(2, settings.MouseMovementSampling.MinimumDistancePixels);
    }

    [Fact]
    public async Task SaveAsync_Then_LoadAsync_Round_Trips_Settings()
    {
        var repository = CreateRepository(out _);
        var settings = new ApplicationSettings
        {
            MouseMovementSampling = new MouseMovementSamplingSettings { MinimumIntervalMilliseconds = 20, MinimumDistancePixels = 5 }
        };

        await repository.SaveAsync(settings);
        var loaded = await repository.LoadAsync();

        Assert.Equal(20, loaded.MouseMovementSampling.MinimumIntervalMilliseconds);
        Assert.Equal(5, loaded.MouseMovementSampling.MinimumDistancePixels);
    }

    [Fact]
    public async Task LoadAsync_Returns_Default_Hotkeys_When_File_Does_Not_Exist()
    {
        var repository = CreateRepository(out _);

        var settings = await repository.LoadAsync();

        Assert.Equal(4, settings.Hotkeys.Count);
        Assert.Contains(settings.Hotkeys, h => h.Id == HotkeyIds.AutoClickerToggle && h.Key == VirtualKey.F6);
        Assert.Contains(settings.Hotkeys, h => h.Id == HotkeyIds.EmergencyStop && h.Key == VirtualKey.F9);
    }

    [Fact]
    public async Task SaveAsync_Then_LoadAsync_Round_Trips_Custom_Hotkeys()
    {
        var repository = CreateRepository(out _);
        var settings = new ApplicationSettings
        {
            Hotkeys =
            [
                new HotkeyDefinition { Id = HotkeyIds.AutoClickerToggle, Key = VirtualKey.F1, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt },
                new HotkeyDefinition { Id = HotkeyIds.MacroRecorderToggle, Key = VirtualKey.F7 },
                new HotkeyDefinition { Id = HotkeyIds.MacroPlaybackToggle, Key = VirtualKey.F8 },
                new HotkeyDefinition { Id = HotkeyIds.EmergencyStop, Key = VirtualKey.F9 }
            ]
        };

        await repository.SaveAsync(settings);
        var loaded = await repository.LoadAsync();

        var autoClickerHotkey = loaded.Hotkeys.Single(h => h.Id == HotkeyIds.AutoClickerToggle);
        Assert.Equal(VirtualKey.F1, autoClickerHotkey.Key);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, autoClickerHotkey.Modifiers);
    }

    [Fact]
    public async Task LoadAsync_Falls_Back_To_Defaults_When_File_Is_Corrupted()
    {
        var repository = CreateRepository(out var logger);
        var paths = new m4AutoClicker.Infrastructure.ApplicationPaths(_tempRoot);
        Directory.CreateDirectory(paths.RootDirectory);
        await File.WriteAllTextAsync(paths.SettingsFilePath, "{ bu geçerli bir json değil");

        var settings = await repository.LoadAsync();

        Assert.Equal(8, settings.MouseMovementSampling.MinimumIntervalMilliseconds);
        Assert.NotEmpty(logger.ErrorMessages);
    }

    [Fact]
    public async Task SaveAsync_Handles_Many_Concurrent_Calls_Without_Throwing()
    {
        var repository = CreateRepository(out _);

        var tasks = Enumerable.Range(0, 20)
            .Select(i => repository.SaveAsync(new ApplicationSettings
            {
                MouseMovementSampling = new MouseMovementSamplingSettings { MinimumIntervalMilliseconds = i, MinimumDistancePixels = i }
            }))
            .ToArray();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));

        Assert.Null(exception);
        var loaded = await repository.LoadAsync();
        Assert.NotNull(loaded);
    }
}
