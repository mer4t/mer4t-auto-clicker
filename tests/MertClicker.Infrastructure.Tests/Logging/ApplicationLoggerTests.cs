using MertClicker.Infrastructure.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MertClicker.Infrastructure.Tests.Logging;

public sealed class ApplicationLoggerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "MertClickerTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Construction_Deletes_Log_Files_Older_Than_Retention_Period()
    {
        var paths = new ApplicationPaths(_tempRoot);
        Directory.CreateDirectory(paths.LogsDirectory);

        var oldFile = Path.Combine(paths.LogsDirectory, $"mertclicker-{DateTime.Now.AddDays(-30):yyyy-MM-dd}.log");
        var recentFile = Path.Combine(paths.LogsDirectory, $"mertclicker-{DateTime.Now.AddDays(-1):yyyy-MM-dd}.log");
        File.WriteAllText(oldFile, "eski kayıt");
        File.WriteAllText(recentFile, "yeni kayıt");

        _ = new ApplicationLogger(NullLoggerFactory.Instance, paths);

        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(recentFile));
    }

    [Fact]
    public void Construction_Deletes_Other_Expired_Files_Even_If_One_Is_Locked()
    {
        // Bir dosya başka bir işlem tarafından kilitliyse (silme paylaşımı olmadan açılmışsa),
        // temizlik o dosyayı atlayıp diğer süresi dolmuş dosyaları yine de silmeli.
        var paths = new ApplicationPaths(_tempRoot);
        Directory.CreateDirectory(paths.LogsDirectory);

        var lockedFile = Path.Combine(paths.LogsDirectory, $"mertclicker-{DateTime.Now.AddDays(-30):yyyy-MM-dd}.log");
        var deletableFile = Path.Combine(paths.LogsDirectory, $"mertclicker-{DateTime.Now.AddDays(-40):yyyy-MM-dd}.log");
        File.WriteAllText(lockedFile, "kilitli");
        File.WriteAllText(deletableFile, "silinebilir");

        using var lockStream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        _ = new ApplicationLogger(NullLoggerFactory.Instance, paths);

        Assert.True(File.Exists(lockedFile));
        Assert.False(File.Exists(deletableFile));
    }

    [Fact]
    public void Construction_Ignores_Files_That_Do_Not_Match_Expected_Naming_Pattern()
    {
        var paths = new ApplicationPaths(_tempRoot);
        Directory.CreateDirectory(paths.LogsDirectory);

        var unrelatedFile = Path.Combine(paths.LogsDirectory, "readme.log");
        File.WriteAllText(unrelatedFile, "silinmemeli");

        _ = new ApplicationLogger(NullLoggerFactory.Instance, paths);

        Assert.True(File.Exists(unrelatedFile));
    }
}
