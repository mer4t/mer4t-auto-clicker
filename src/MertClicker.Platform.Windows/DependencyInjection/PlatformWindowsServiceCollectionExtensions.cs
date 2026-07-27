using MertClicker.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace MertClicker.Platform.Windows.DependencyInjection;

public static class PlatformWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddMertClickerPlatformWindows(this IServiceCollection services)
    {
        services.AddSingleton<IHighResolutionClock, WindowsHighResolutionClock>();
        services.AddSingleton<IDisplayService, WindowsDisplayService>();
        services.AddSingleton<WindowsCoordinateConverter>();
        services.AddSingleton<IInputInjector, WindowsInputInjector>();
        services.AddSingleton<IInputCaptureProvider, WindowsMouseHook>();
        services.AddSingleton<IKeyboardCaptureProvider, WindowsKeyboardHook>();
        services.AddSingleton<IHotkeyService, WindowsHotkeyService>();
        services.AddSingleton<ICursorPositionProvider, WindowsCursorPositionProvider>();
        services.AddSingleton<ITrayIconService, WindowsTrayIconService>();

        return services;
    }
}
