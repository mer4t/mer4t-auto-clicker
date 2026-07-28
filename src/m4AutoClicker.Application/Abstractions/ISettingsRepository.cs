using m4AutoClicker.Application.Models;

namespace m4AutoClicker.Application.Abstractions;

public interface ISettingsRepository
{
    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
