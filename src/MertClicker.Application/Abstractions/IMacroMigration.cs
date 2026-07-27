using System.Text.Json;

namespace MertClicker.Application.Abstractions;

public interface IMacroMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    JsonDocument Migrate(JsonDocument document);
}
