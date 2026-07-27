using System.Text.Json;
using MertClicker.Application.Abstractions;

namespace MertClicker.Application.Services;

public sealed class MacroMigrationCoordinator
{
    private readonly IReadOnlyList<IMacroMigration> _migrations;

    public MacroMigrationCoordinator(IEnumerable<IMacroMigration> migrations)
    {
        _migrations = migrations.ToList();
    }

    // Girdi belgesi (document) çağıran tarafından sahiplenilir ve burada dispose edilmez. Zincirleme
    // geçişler sırasında oluşan ara belgeler dispose edilir; döndürülen son belgenin sahipliği
    // çağırana geçer.
    public JsonDocument MigrateToLatest(JsonDocument document, int fromVersion, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (fromVersion > targetVersion)
        {
            throw new InvalidOperationException(
                $"Kaynak şema sürümü ({fromVersion}) hedef sürümden ({targetVersion}) daha yeni olamaz.");
        }

        var currentDocument = document;
        var currentVersion = fromVersion;
        var ownsCurrentDocument = false;

        while (currentVersion < targetVersion)
        {
            var migration = _migrations.FirstOrDefault(m => m.FromVersion == currentVersion);
            if (migration is null)
            {
                throw new InvalidOperationException(
                    $"Şema sürümü {currentVersion} için bir makro geçiş (migration) adımı bulunamadı.");
            }

            if (migration.ToVersion <= currentVersion)
            {
                throw new InvalidOperationException(
                    $"Geçiş adımı sürümü ilerletmiyor: {migration.FromVersion} -> {migration.ToVersion}.");
            }

            var migrated = migration.Migrate(currentDocument);

            if (ownsCurrentDocument)
            {
                currentDocument.Dispose();
            }

            currentDocument = migrated;
            currentVersion = migration.ToVersion;
            ownsCurrentDocument = true;
        }

        return currentDocument;
    }
}
