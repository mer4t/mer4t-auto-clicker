using MertClicker.Application.Models;

namespace MertClicker.Application.Abstractions;

public interface ISettingsRepository
{
    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
