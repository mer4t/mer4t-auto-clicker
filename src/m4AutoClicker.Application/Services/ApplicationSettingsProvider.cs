using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;

namespace m4AutoClicker.Application.Services;

public sealed class ApplicationSettingsProvider : IApplicationSettingsProvider
{
    private volatile ApplicationSettings _current = new();

    public ApplicationSettings Current => _current;

    public void Update(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _current = settings;
    }
}
