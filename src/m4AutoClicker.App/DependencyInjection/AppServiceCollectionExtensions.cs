using m4AutoClicker.App.ViewModels;
using m4AutoClicker.App.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace m4AutoClicker.App.DependencyInjection;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection Addm4AutoClickerApp(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<AutoClickerViewModel>();
        services.AddSingleton<MacroRecorderViewModel>();
        services.AddSingleton<MyMacrosViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<AboutViewModel>();

        return services;
    }
}
