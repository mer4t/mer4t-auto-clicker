using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Infrastructure.Logging;
using m4AutoClicker.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace m4AutoClicker.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection Addm4AutoClickerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationPaths>();
        services.AddSingleton<IApplicationLogger, ApplicationLogger>();
        services.AddSingleton<IMacroRepository, JsonMacroRepository>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<ILogReader, FileLogReader>();

        return services;
    }
}
