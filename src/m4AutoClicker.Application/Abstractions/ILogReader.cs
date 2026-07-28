namespace m4AutoClicker.Application.Abstractions;

public interface ILogReader
{
    Task<IReadOnlyList<string>> ReadRecentLinesAsync(int maxLines = 200, CancellationToken cancellationToken = default);
}
