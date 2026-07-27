using MertClicker.Application.Abstractions;
using MertClicker.Application.DependencyInjection;
using MertClicker.Application.Services;
using MertClicker.Application.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace MertClicker.Application.Tests.DependencyInjection;

public class ApplicationServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IInputInjector, FakeInputInjector>();
        services.AddSingleton<IHighResolutionClock, FakeHighResolutionClock>();
        services.AddSingleton<IApplicationLogger, FakeApplicationLogger>();
        services.AddSingleton<IInputCaptureProvider, FakeInputCaptureProvider>();
        services.AddSingleton<IKeyboardCaptureProvider, FakeKeyboardCaptureProvider>();
        services.AddSingleton<IDisplayService, FakeDisplayService>();

        services.AddMertClickerApplication();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void All_Registered_Application_Services_Can_Be_Constructed()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<AutomationEngine>());
        Assert.NotNull(provider.GetRequiredService<AutoClickerService>());
        Assert.NotNull(provider.GetRequiredService<MacroRecorder>());
        Assert.NotNull(provider.GetRequiredService<IMacroPlayer>());
        Assert.NotNull(provider.GetRequiredService<MacroValidator>());
        Assert.NotNull(provider.GetRequiredService<MacroOptimizer>());
        Assert.NotNull(provider.GetRequiredService<ICoordinateResolver>());
        Assert.NotNull(provider.GetRequiredService<PlaybackScheduler>());
        Assert.NotNull(provider.GetRequiredService<MacroMigrationCoordinator>());
    }
}
