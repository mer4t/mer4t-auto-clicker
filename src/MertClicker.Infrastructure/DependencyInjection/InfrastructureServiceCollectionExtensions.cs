using MertClicker.Application.Abstractions;
using MertClicker.Infrastructure.Logging;
using MertClicker.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MertClicker.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMertClickerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationPaths>();
        services.AddSingleton<IApplicationLogger, ApplicationLogger>();
        services.AddSingleton<IMacroRepository, JsonMacroRepository>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<ILogReader, FileLogReader>();

        return services;
    }
}
