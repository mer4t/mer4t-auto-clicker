using m4AutoClicker.Application.Abstractions;

namespace m4AutoClicker.Infrastructure.Logging;

public sealed class FileLogReader : ILogReader
{
    private readonly ApplicationPaths _paths;

    public FileLogReader(ApplicationPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<string>> ReadRecentLinesAsync(int maxLines = 200, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_paths.LogsDirectory))
        {
            return [];
        }

        var latestFile = Directory.EnumerateFiles(_paths.LogsDirectory, "m4autoclicker-*.log")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latestFile is null)
        {
            return [];
        }

        var allLines = new List<string>();

        try
        {
            // ApplicationLogger aynı dosyayı eşzamanlı olarak açıp kapatabildiği için FileShare.ReadWrite
            // ile açılır; aksi halde okuma sırasında paylaşım ihlali (IOException) oluşabilir.
            using var stream = new FileStream(latestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                allLines.Add(line);
            }
        }
        catch (IOException)
        {
            return [];
        }

        return allLines.Count <= maxLines ? allLines : allLines.GetRange(allLines.Count - maxLines, maxLines);
    }
}
