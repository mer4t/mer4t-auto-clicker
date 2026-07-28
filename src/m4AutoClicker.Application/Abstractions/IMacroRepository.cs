using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Abstractions;

public interface IMacroRepository
{
    Task<IReadOnlyList<MacroSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Macro?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Macro macro, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ExportAsync(Guid id, string destinationFilePath, CancellationToken cancellationToken = default);

    Task<Macro> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default);
}
