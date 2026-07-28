using System.Text.Json;

namespace m4AutoClicker.Application.Abstractions;

public interface IMacroMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    JsonDocument Migrate(JsonDocument document);
}
