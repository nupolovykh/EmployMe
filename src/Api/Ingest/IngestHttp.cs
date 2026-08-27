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

    /// <summary>
    /// Try-parsed, not cast: a JSON number that is fractional or wider than the
    /// target throws from Get*, and one malformed salary would take down the
    /// whole run for that source rather than dropping a single field.
    /// </summary>
    public static int? Int(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    /// <inheritdoc cref="Int"/>
    public static long? Long(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var parsed)
            ? parsed
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
