using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Tests.Support;

/// <summary>Reads the committed spike responses copied next to the test binary.</summary>
public static class Spikes
{
    public static string Read(string source) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spikes", source, "response.json"));

    /// <summary>
    /// spikes/lever/response.json is not a live body: it wraps three companies'
    /// truncated postings under <c>companies.{token}.postings</c>, with the note
    /// that the live root is a flat array. This hands back one company's array
    /// in that live shape.
    /// </summary>
    public static string LeverPostings(string token)
    {
        var root = JsonNode.Parse(Read("lever"))!;
        return root["companies"]![token]!["postings"]!.ToJsonString();
    }

    public static JsonDocument Parse(string source) => JsonDocument.Parse(Read(source));
}
