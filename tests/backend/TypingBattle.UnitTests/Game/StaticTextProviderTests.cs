using TypingBattle.Api.Game;

namespace TypingBattle.UnitTests.Game;

[Trait("Category", "Unit")]
public class StaticTextProviderTests
{
    [Fact]
    public void There_are_enough_texts_with_unique_ids()
    {
        var texts = StaticTextProvider.Texts;

        Assert.True(texts.Count >= 15);
        Assert.Equal(texts.Count, texts.Select(t => t.Id).Distinct().Count());
        Assert.Equal(texts.Count, texts.Select(t => t.Text).Distinct().Count());
    }

    [Fact]
    public void Every_text_is_a_single_line_of_reasonable_length()
    {
        foreach (var text in StaticTextProvider.Texts)
        {
            Assert.InRange(text.Text.Length, 80, 260);
            Assert.DoesNotContain('\n', text.Text);
            Assert.DoesNotContain('\r', text.Text);
            Assert.Equal(text.Text.Trim(), text.Text);
            Assert.DoesNotContain("  ", text.Text); // sin espacios dobles: confunden al teclear
        }
    }

    [Fact]
    public void Next_returns_one_of_the_texts()
    {
        var provider = new StaticTextProvider();

        for (var i = 0; i < 50; i++)
        {
            Assert.Contains(provider.Next(), StaticTextProvider.Texts);
        }
    }
}
