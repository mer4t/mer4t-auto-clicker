using m4AutoClicker.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace m4AutoClicker.Infrastructure.Logging;

// Konsol/Debug çıktısına ek olarak, Kayıtlar (Logs) ekranının okuyabilmesi için ApplicationPaths.
// LogsDirectory altında güne göre dönen bir düz metin dosyasına da yazar (bkz. FileLogReader).
public sealed class ApplicationLogger : IApplicationLogger
{
    private const int RetentionDays = 14;

    private readonly ILogger _logger;
    private readonly ApplicationPaths _paths;
    private readonly Lock _fileLock = new();

    public ApplicationLogger(ILoggerFactory loggerFactory, ApplicationPaths paths)
    {
        _logger = loggerFactory.CreateLogger("m4AutoClicker");
        _paths = paths;

        DeleteExpiredLogFiles();
    }

    private void DeleteExpiredLogFiles()
    {
        if (!Directory.Exists(_paths.LogsDirectory))
        {
            return;
        }

        var cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-RetentionDays);

        IEnumerable<string> filePaths;
        try
        {
            filePaths = Directory.EnumerateFiles(_paths.LogsDirectory, "m4autoclicker-*.log").ToList();
        }
        catch
        {
            // Dizin listelenemezse (ör. izin sorunu) uygulama başlatmayı engellememeli.
            return;
        }

        foreach (var filePath in filePaths)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var datePart = fileName["m4autoclicker-".Length..];

                if (DateOnly.TryParseExact(datePart, "yyyy-MM-dd", out var fileDate) && fileDate < cutoff)
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Bu dosya silinemezse (ör. başka bir işlem tarafından kilitli) yalnızca o dosya
                // atlanır; diğer süresi dolmuş dosyaların silinmesi engellenmez. Bir sonraki
                // başlangıçta tekrar denenir.
            }
        }
    }

    public void LogDebug(string message, params object?[] args)
    {
        _logger.LogDebug(message, args);
        AppendToFile("DEBUG", null, message, args);
    }

    public void LogInformation(string message, params object?[] args)
    {
        _logger.LogInformation(message, args);
        AppendToFile("INFO", null, message, args);
    }

    public void LogWarning(string message, params object?[] args)
    {
        _logger.LogWarning(message, args);
        AppendToFile("WARN", null, message, args);
    }

    public void LogError(Exception? exception, string message, params object?[] args)
    {
        _logger.LogError(exception, message, args);
        AppendToFile("ERROR", exception, message, args);
    }

    private void AppendToFile(string level, Exception? exception, string message, object?[] args)
    {
        try
        {
            string formatted;
            try
            {
                formatted = args.Length == 0 ? message : string.Format(message, args);
            }
            catch (FormatException)
            {
                formatted = message;
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {formatted}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            Directory.CreateDirectory(_paths.LogsDirectory);
            var filePath = Path.Combine(_paths.LogsDirectory, $"m4autoclicker-{DateTime.Now:yyyy-MM-dd}.log");

            lock (_fileLock)
            {
                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(line);
            }
        }
        catch
        {
            // Log dosyasına yazılamaması (ör. disk dolu, izin sorunu) uygulamayı çökertmemeli.
        }
    }
}
