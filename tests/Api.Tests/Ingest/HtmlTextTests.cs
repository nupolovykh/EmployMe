using Api.Ingest;

namespace Api.Tests.Ingest;

public class HtmlTextTests
{
    [Fact]
    public void Strips_markup_and_decodes_entities() =>
        Assert.Equal("Wir suchen & finden", HtmlText.ToPlainText("<p>Wir <b>suchen</b> &amp; finden</p>"));

    [Fact]
    public void Double_escaped_greenhouse_content_ends_up_as_text()
    {
        // Greenhouse escapes its markup twice; numeric entities must not
        // survive the strip and be expanded back into tags afterwards.
        var plain = HtmlText.ToPlainText("&lt;div&gt;Hello&lt;/div&gt; &#60;b&#62;x&#60;/b&#62;");

        Assert.DoesNotContain("<", plain);
        Assert.Contains("Hello", plain);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_is_null(string? html) => Assert.Null(HtmlText.ToPlainText(html));
}
