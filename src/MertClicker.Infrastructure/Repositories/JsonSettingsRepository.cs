using System.Text.Json;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;

namespace MertClicker.Infrastructure.Repositories;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly ApplicationPaths _paths;
    private readonly IApplicationLogger _logger;

    // Ayar dosyası tek bir sabit yola yazıldığı için, eşzamanlı iki SaveAsync çağrısı (ör. Kaydet
    // butonuna hızlıca iki kez tıklanması) aynı temp dosyasında yarışabilir; bu kilit onu önler.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonSettingsRepository(ApplicationPaths paths, IApplicationLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.SettingsFilePath))
        {
            return new ApplicationSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_paths.SettingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(
                stream, JsonSerializationDefaults.Options, cancellationToken);

            return settings ?? new ApplicationSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Kullanıcı ayar dosyasını elle bozmuş olsa bile uygulama açılabilmeli; varsayılanlara
            // dönülür.
            _logger.LogError(ex, "Ayar dosyası okunamadı, varsayılan ayarlar kullanılıyor: {0}.", _paths.SettingsFilePath);
            return new ApplicationSettings();
        }
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(_paths.RootDirectory);

        var tempFilePath = _paths.SettingsFilePath + ".tmp";

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using (var stream = File.Create(tempFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonSerializationDefaults.Options, cancellationToken);
            }

            File.Move(tempFilePath, _paths.SettingsFilePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogInformation("Ayarlar diske kaydedildi: {0}.", _paths.SettingsFilePath);
    }
}
