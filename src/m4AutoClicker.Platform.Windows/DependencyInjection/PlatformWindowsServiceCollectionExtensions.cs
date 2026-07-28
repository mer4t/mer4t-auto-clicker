using m4AutoClicker.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace m4AutoClicker.Platform.Windows.DependencyInjection;

public static class PlatformWindowsServiceCollectionExtensions
{
    public static IServiceCollection Addm4AutoClickerPlatformWindows(this IServiceCollection services)
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
