using MertClicker.Application.Abstractions;
using MertClicker.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MertClicker.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddMertClickerApplication(this IServiceCollection services)
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
