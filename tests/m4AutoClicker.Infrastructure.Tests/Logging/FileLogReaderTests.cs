using m4AutoClicker.Infrastructure.Logging;

namespace m4AutoClicker.Infrastructure.Tests.Logging;

public sealed class FileLogReaderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "m4AutoClickerTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadRecentLinesAsync_Returns_Empty_When_No_Logs_Directory()
    {
        var paths = new ApplicationPaths(_tempRoot);
        var reader = new FileLogReader(paths);

        var lines = await reader.ReadRecentLinesAsync();

        Assert.Empty(lines);
    }

    [Fact]
    public async Task ReadRecentLinesAsync_Returns_Lines_Written_By_ApplicationLogger()
    {
        var paths = new ApplicationPaths(_tempRoot);
        var logger = new ApplicationLogger(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, paths);
        var reader = new FileLogReader(paths);

        logger.LogInformation("Merhaba {0}", "dünya");
        logger.LogWarning("Dikkat: {0}", 42);
        logger.LogError(new InvalidOperationException("test hatası"), "Bir hata oluştu");

        var lines = await reader.ReadRecentLinesAsync();

        Assert.Contains(lines, l => l.Contains("[INFO]") && l.Contains("Merhaba dünya"));
        Assert.Contains(lines, l => l.Contains("[WARN]") && l.Contains("Dikkat: 42"));
        Assert.Contains(lines, l => l.Contains("[ERROR]") && l.Contains("Bir hata oluştu"));
    }

    [Fact]
    public async Task ReadRecentLinesAsync_Limits_To_Requested_Line_Count()
    {
        var paths = new ApplicationPaths(_tempRoot);
        var logger = new ApplicationLogger(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, paths);
        var reader = new FileLogReader(paths);

        for (var i = 0; i < 10; i++)
        {
            logger.LogInformation("Satır {0}", i);
        }

        var lines = await reader.ReadRecentLinesAsync(maxLines: 3);

        Assert.Equal(3, lines.Count);
        Assert.Contains("Satır 9", lines[^1]);
    }
}
