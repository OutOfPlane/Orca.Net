using System.Text.Json;

namespace OrcaCli;

/// <summary>Central place for the JSON contracts printed with <c>--json</c>.</summary>
internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Write(object value) => Console.Out.WriteLine(JsonSerializer.Serialize(value, Options));
}
