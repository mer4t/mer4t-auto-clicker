using MertClicker.App.ViewModels;
using MertClicker.App.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MertClicker.App.DependencyInjection;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddMertClickerApp(this IServiceCollection services)
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
