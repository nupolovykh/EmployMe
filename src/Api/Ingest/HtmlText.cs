using System.Net;
using System.Text.RegularExpressions;

namespace Api.Ingest;

/// <summary>
/// Three of the four MVP sources hand back HTML descriptions and one hands back
/// plain text. Keyword filtering runs ILIKE over this column, so markup is
/// stored as text: otherwise a search for "div" matches every posting.
/// </summary>
public static partial class HtmlText
{
    public static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(html);

        // Greenhouse escapes its markup a second time, so one pass yields tags
        // rather than text — see spikes/greenhouse/response.json's `content`.
        if (decoded.Contains("&lt;", StringComparison.Ordinal))
        {
            decoded = WebUtility.HtmlDecode(decoded);
        }

        var text = WebUtility.HtmlDecode(TagRegex().Replace(decoded, " "));
        text = WhitespaceRegex().Replace(text, " ").Trim();

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
