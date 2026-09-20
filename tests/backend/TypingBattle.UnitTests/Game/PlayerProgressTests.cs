using TypingBattle.Api.Game;

namespace TypingBattle.UnitTests.Game;

[Trait("Category", "Unit")]
public class PlayerProgressTests
{
    private const string Text = "hola mundo";
    private static readonly DateTime Now = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);

    private static PlayerProgress NewPlayer() => new("user-1", "Ana", 1);

    [Fact]
    public void New_characters_count_as_keystrokes_and_mismatches_as_errors()
    {
        var player = NewPlayer();

        Assert.True(player.Apply(Text, "ho", Now));
        Assert.Equal((2, 0, 2), (player.Keystrokes, player.Errors, player.CorrectChars));

        Assert.True(player.Apply(Text, "hox", Now));
        Assert.Equal((3, 1, 2), (player.Keystrokes, player.Errors, player.CorrectChars));
    }

    [Fact]
    public void Deleting_is_free_but_a_corrected_mistake_still_counts_as_an_error()
    {
        var player = NewPlayer();
        player.Apply(Text, "hox", Now);

        player.Apply(Text, "ho", Now); // borra
        Assert.Equal((3, 1), (player.Keystrokes, player.Errors));

        player.Apply(Text, "hol", Now); // reescribe bien
        Assert.Equal((4, 1, 3), (player.Keystrokes, player.Errors, player.CorrectChars));
    }

    [Fact]
    public void Progress_counts_only_the_correct_prefix()
    {
        var player = NewPlayer();

        player.Apply(Text, "hxla mund", Now);

        Assert.Equal(1, player.CorrectChars);
    }

    [Fact]
    public void An_unchanged_text_reports_no_change()
    {
        var player = NewPlayer();
        player.Apply(Text, "hol", Now);

        Assert.False(player.Apply(Text, "hol", Now));
        Assert.Equal(3, player.Keystrokes);
    }

    [Fact]
    public void Text_beyond_the_target_is_trimmed()
    {
        var player = NewPlayer();

        player.Apply(Text, Text + "de más", Now);

        Assert.Equal(Text, player.Typed);
        Assert.True(player.Finished);
    }

    [Fact]
    public void It_only_finishes_with_the_complete_text_and_no_errors()
    {
        var player = NewPlayer();

        player.Apply(Text, "hola mundx", Now);
        Assert.False(player.Finished);
        Assert.Equal(9, player.CorrectChars);

        player.Apply(Text, "hola mund", Now);
        Assert.False(player.Finished);

        player.Apply(Text, Text, Now.AddSeconds(5));
        Assert.True(player.Finished);
        Assert.Equal(Now.AddSeconds(5), player.FinishedAt);
        Assert.Equal(1, player.Errors);
    }

    [Fact]
    public void After_finishing_nothing_changes()
    {
        var player = NewPlayer();
        player.Apply(Text, Text, Now);

        Assert.False(player.Apply(Text, "otra cosa", Now.AddSeconds(1)));

        Assert.Equal(Text, player.Typed);
        Assert.Equal(Now, player.FinishedAt);
    }

    [Fact]
    public void A_player_is_connected_while_any_connection_remains()
    {
        var player = NewPlayer();
        Assert.False(player.Connected);

        player.Connections.Add("c1");
        player.Connections.Add("c2");
        player.Connections.Remove("c1");

        Assert.True(player.Connected);
    }
}
