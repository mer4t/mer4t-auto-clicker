using System.Text.Json;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Services;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Infrastructure.Repositories;

public sealed class JsonMacroRepository : IMacroRepository
{
    // Makrolar hâlâ tek şema sürümünde (Aşama 5'te MacroRecorder tarafından üretilen SchemaVersion=1);
    // bu sabit, ileride yeni bir şema sürümü çıktığında güncellenmeli ve karşılık gelen bir
    // IMacroMigration eklenmelidir.
    private const int CurrentSchemaVersion = 1;

    private readonly ApplicationPaths _paths;
    private readonly MacroMigrationCoordinator _migrationCoordinator;
    private readonly IApplicationLogger _logger;

    // SaveAsync temp dosya + atomik taşıma kullanır, ancak aynı makro (aynı Id) eşzamanlı olarak
    // iki kez kaydedilirse iki çağrı da AYNI temp dosya yoluna yazar; bu kilit olmadan bir yazma
    // diğerini yarıda kesebilir veya kaybolmuş bir File.Move istisnasına yol açabilir.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonMacroRepository(ApplicationPaths paths, MacroMigrationCoordinator migrationCoordinator, IApplicationLogger logger)
    {
        _paths = paths;
        _migrationCoordinator = migrationCoordinator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MacroSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<MacroSummary>();

        if (!Directory.Exists(_paths.MacrosDirectory))
        {
            return summaries;
        }

        foreach (var filePath in Directory.EnumerateFiles(_paths.MacrosDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var macro = await TryReadMacroFileAsync(filePath, cancellationToken);
            if (macro is null)
            {
                continue;
            }

            summaries.Add(new MacroSummary
            {
                Id = macro.Id,
                Name = macro.Name,
                Description = macro.Description,
                UpdatedAtUtc = macro.UpdatedAtUtc,
                ActionCount = macro.Actions.Count,
                Tags = macro.Tags
            });
        }

        return summaries.OrderByDescending(s => s.UpdatedAtUtc).ToList();
    }

    public async Task<Macro?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id);
        return File.Exists(filePath) ? await TryReadMacroFileAsync(filePath, cancellationToken) : null;
    }

    public async Task SaveAsync(Macro macro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(macro);

        Directory.CreateDirectory(_paths.MacrosDirectory);

        var filePath = GetFilePath(macro.Id);
        // Aynı makro Id'si eşzamanlı olarak iki kez kaydedilirse ikisi de bu yola yazar; kilit
        // olmadan biri diğerinin temp dosyasını yarıda kesebilir veya File.Move başarısız olabilir.
        var tempFilePath = filePath + ".tmp";

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Ani kapanma/çökme sırasında yarım yazılmış bir dosya kalmaması için geçici dosyaya yazıp
            // ardından yerine taşıyoruz (atomik değiştirme).
            await using (var stream = File.Create(tempFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, macro, JsonSerializationDefaults.Options, cancellationToken);
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }

        _logger.LogInformation("Makro diske kaydedildi: {0} ({1}).", macro.Name, macro.Id);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Makro silindi: {0}.", id);
        }

        return Task.CompletedTask;
    }

    public async Task ExportAsync(Guid id, string destinationFilePath, CancellationToken cancellationToken = default)
    {
        var macro = await GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Dışa aktarılacak makro bulunamadı: {id}.");

        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        // İptal veya diskte bir hata (dolu disk, kilitli dosya vb.) yarım yazılmış/bozuk bir dosya
        // bırakmasın diye geçici dosyaya yazılıp ardından hedefe atomik olarak taşınır. Kullanıcı
        // aynı yola daha önce geçerli bir dışa aktarım yapmışsa, o dosya başarısız bir denemeyle
        // bozulmamış olur.
        var tempFilePath = destinationFilePath + ".tmp";
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, macro, JsonSerializationDefaults.Options, cancellationToken);
        }

        File.Move(tempFilePath, destinationFilePath, overwrite: true);

        _logger.LogInformation("Makro dışa aktarıldı: {0} -> {1}.", macro.Name, destinationFilePath);
    }

    public async Task<Macro> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var macro = await TryReadMacroFileAsync(sourceFilePath, cancellationToken)
            ?? throw new InvalidOperationException($"Makro dosyası okunamadı veya geçersiz: {sourceFilePath}.");

        // Var olan (veya daha önce içe aktarılmış) bir makroyla çakışmaması için her içe aktarmada
        // yeni bir kimlik atanır.
        var imported = macro with { Id = Guid.NewGuid(), UpdatedAtUtc = DateTime.UtcNow };
        await SaveAsync(imported, cancellationToken);

        _logger.LogInformation("Makro içe aktarıldı: {0} <- {1}.", imported.Name, sourceFilePath);
        return imported;
    }

    private string GetFilePath(Guid id) => Path.Combine(_paths.MacrosDirectory, $"{id}.json");

    private async Task<Macro?> TryReadMacroFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await ParseAsync(filePath, cancellationToken);

            var storedVersion = document.RootElement.TryGetProperty("schemaVersion", out var versionElement)
                ? versionElement.GetInt32()
                : CurrentSchemaVersion;

            if (storedVersion == CurrentSchemaVersion)
            {
                return document.Deserialize<Macro>(JsonSerializationDefaults.Options);
            }

            using var migrated = _migrationCoordinator.MigrateToLatest(document, storedVersion, CurrentSchemaVersion);
            return migrated.Deserialize<Macro>(JsonSerializationDefaults.Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogError(ex, "Makro dosyası okunamadı, atlanıyor: {0}.", filePath);
            return null;
        }
    }

    private static async Task<JsonDocument> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
