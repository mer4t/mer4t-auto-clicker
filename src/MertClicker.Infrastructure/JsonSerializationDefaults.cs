using System.Text.Json;

namespace MertClicker.Infrastructure;

internal static class JsonSerializationDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
