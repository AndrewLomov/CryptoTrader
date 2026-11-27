using System.Text.Json;

namespace CryptoTrader.Infrastructure.Persistence;

internal static class PersistenceJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static string SerializeRuntime(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, value.GetType(), Options);
    }

    public static T DeserializeRequired<T>(string json, string column)
    {
        var value = JsonSerializer.Deserialize<T>(json, Options);
        if (value is null)
        {
            throw new InvalidOperationException($"Column '{column}' did not contain a JSON document.");
        }

        return value;
    }
}
