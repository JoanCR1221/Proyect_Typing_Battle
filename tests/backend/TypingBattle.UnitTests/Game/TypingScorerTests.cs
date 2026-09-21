using TypingBattle.Api.Game;

namespace TypingBattle.UnitTests.Game;

[Trait("Category", "Unit")]
public class TypingScorerTests
{
    [Theory]
    [InlineData(100, 60, 20.0)] // 100 caracteres = 20 «palabras» en un minuto
    [InlineData(300, 60, 60.0)]
    [InlineData(150, 30, 60.0)]
    [InlineData(0, 30, 0.0)]
    public void Wpm_counts_five_correct_characters_as_one_word(int correctChars, int seconds, double expected) =>
        Assert.Equal(expected, TypingScorer.Wpm(correctChars, TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Wpm_never_divides_by_less_than_one_second()
    {
        // 5 letras en 0,1 s darían 600 ppm; con el mínimo de 1 s son 60 ppm.
        Assert.Equal(60.0, TypingScorer.Wpm(5, TimeSpan.FromMilliseconds(100)));
        Assert.Equal(60.0, TypingScorer.Wpm(5, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(100, 0, 100.0)]
    [InlineData(100, 5, 95.0)]
    [InlineData(3, 1, 66.7)]
    [InlineData(10, 10, 0.0)]
    public void Accuracy_is_the_share_of_correct_keystrokes(int keystrokes, int errors, double expected) =>
        Assert.Equal(expected, TypingScorer.Accuracy(keystrokes, errors));

    [Fact]
    public void Nobody_who_did_not_type_has_perfect_accuracy() =>
        Assert.Equal(0, TypingScorer.Accuracy(0, 0));

    [Theory]
    [InlineData(0, 100, 0.0)]
    [InlineData(50, 200, 25.0)]
    [InlineData(200, 100, 100.0)] // nunca pasa de 100
    [InlineData(5, 0, 0.0)]
    public void Progress_is_the_percentage_of_the_text_typed_correctly(int correct, int length, double expected) =>
        Assert.Equal(expected, TypingScorer.Progress(correct, length));

    [Theory]
    [InlineData(60.0, 100.0, 600)]
    [InlineData(62.4, 96.1, 600)] // 599,66 se redondea
    [InlineData(30.0, 50.0, 150)]
    [InlineData(0.0, 100.0, 0)]
    public void Score_is_speed_times_accuracy_times_ten(double wpm, double accuracy, int expected) =>
        Assert.Equal(expected, TypingScorer.Score(wpm, accuracy));
}
