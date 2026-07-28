using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace m4AutoClicker.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection Addm4AutoClickerApplication(this IServiceCollection services)
    {
        services.AddSingleton<AutomationEngine>();
        services.AddSingleton<AutoClickerService>();
        services.AddSingleton<MacroRecorder>();
        services.AddSingleton<IMacroPlayer, MacroPlayer>();
        services.AddSingleton<MacroValidator>();
        services.AddSingleton<MacroOptimizer>();
        services.AddSingleton<ICoordinateResolver, CoordinateResolver>();
        services.AddSingleton<PlaybackScheduler>();
        services.AddSingleton<MacroMigrationCoordinator>();
        services.AddSingleton<IEmergencyStopService, EmergencyStopCoordinator>();
        services.AddSingleton<HotkeyCoordinatorService>();
        services.AddSingleton<IApplicationSettingsProvider, ApplicationSettingsProvider>();

        return services;
    }
}
