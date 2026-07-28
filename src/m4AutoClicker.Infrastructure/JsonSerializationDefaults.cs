using System.Text.Json;

namespace m4AutoClicker.Infrastructure;

internal static class JsonSerializationDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
