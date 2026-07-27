using System.Text.Json;
using MertClicker.Application.Abstractions;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeMacroMigration : IMacroMigration
{
    public required int FromVersion { get; init; }

    public required int ToVersion { get; init; }

    // Migrate her çağrıldığında yeni bir JsonDocument döndürür; bu sayede çağıranın ara belgeleri
    // doğru şekilde dispose ettiğini (MacroMigrationCoordinator testlerinde) doğrulayabiliriz.
    public Func<JsonDocument, JsonDocument>? OnMigrate { get; set; }

    public int CallCount { get; private set; }

    public JsonDocument Migrate(JsonDocument document)
    {
        CallCount++;
        return OnMigrate?.Invoke(document) ?? JsonDocument.Parse($"{{\"schemaVersion\":{ToVersion}}}");
    }
}
