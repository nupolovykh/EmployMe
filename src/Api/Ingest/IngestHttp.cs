using System.Text.Json;

namespace Api.Ingest;

public static class IngestHttp
{
    public const string ClientName = "ingest";

    public static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public static string? String(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static int? Int(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    public static long? Long(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;

    /// <summary>
    /// Normalized to UTC: Npgsql rejects any non-zero offset for `timestamptz`,
    /// and Greenhouse stamps `first_published` in the board's local offset.
    /// </summary>
    public static DateTimeOffset? Timestamp(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    /// <summary>Identifiers arrive as a JSON number on some sources and a string on others.</summary>
    public static string? Identifier(this JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }
}
