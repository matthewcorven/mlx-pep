namespace MlxPep.Core;

using System.Text.Json;

internal static class JsonValueConverter
{
    public static object? ConvertToObject(object? value)
    {
        if (value is JsonElement elem)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(elem.GetRawText()),
                JsonValueKind.Array => elem.EnumerateArray().Select(item => ConvertToObject(item) ?? string.Empty).ToList(),
                JsonValueKind.String => elem.GetString(),
                JsonValueKind.Number when elem.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when elem.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number => elem.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => null
            };
        }

        return value;
    }

    public static Dictionary<string, object>? AsDictionary(object? value)
    {
        return ConvertToObject(value) as Dictionary<string, object>;
    }

    public static int? AsInt(object? value)
    {
        value = ConvertToObject(value);
        return value switch
        {
            int intValue => intValue,
            long longValue => Convert.ToInt32(longValue),
            double doubleValue => Convert.ToInt32(doubleValue),
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => null
        };
    }

    public static string? AsString(object? value)
    {
        return ConvertToObject(value)?.ToString();
    }
}
