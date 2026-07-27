using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;

namespace MertClicker.Application.Services;

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
