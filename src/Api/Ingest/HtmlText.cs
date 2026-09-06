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

        // Decoded to a fixed point *before* anything is stripped. Greenhouse
        // escapes its markup twice (see spikes/greenhouse/response.json's
        // `content`), and the previous version handled that one case by looking
        // for "&lt;" — which numeric entities like &#60; do not contain. Those
        // survived the strip and were then expanded by a final decode, putting
        // real markup into a column whose whole purpose is to hold plain text.
        //
        // Bounded rather than looping: three passes cover every depth seen, and a
        // pathological input cannot spin here.
        var decoded = html;

        for (var pass = 0; pass < 3; pass++)
        {
            var next = WebUtility.HtmlDecode(decoded);

            if (next == decoded)
            {
                break;
            }

            decoded = next;
        }

        // Stripping is last, so nothing can reintroduce a tag afterwards. The
        // cost is that an escaped tag written deliberately in prose — "experience
        // with &lt;div&gt; layouts" — is removed along with the real ones. For a
        // column that exists to be searched with ILIKE that is the better trade:
        // losing a word beats storing markup that the previous order let through.
        var text = WhitespaceRegex().Replace(TagRegex().Replace(decoded, " "), " ").Trim();

        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
