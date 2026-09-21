using System.Text.Json;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Results;

[Trait("Category", "Unit")]
public class ResultValidatorTests
{
    private static readonly DateTime Start = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddMinutes(1);

    private static SaveResultRequest ValidRequest() => new(
        MatchId: "match-001",
        GameType: "typing",
        Players: [new("user-001", "Ana", 850), new("user-002", "Luis", 620)],
        StartedAt: Start,
        FinishedAt: End,
        WinnerUserId: "user-001",
        Metadata: null);

    private static Dictionary<string, string[]> ExpectErrors(SaveResultRequest request)
    {
        Assert.False(ResultValidator.TryValidate(request, out var valid, out var errors));
        Assert.Null(valid);
        return errors;
    }

    [Fact]
    public void A_valid_request_passes_and_is_normalized()
    {
        var request = ValidRequest() with { MatchId = "  match-001  ", GameType = "TYPING" };

        Assert.True(ResultValidator.TryValidate(request, out var valid, out var errors));

        Assert.Empty(errors);
        Assert.NotNull(valid);
        Assert.Equal("match-001", valid.MatchId);
        Assert.Equal(["user-001", "user-002"], valid.Players.Select(p => p.UserId));
        Assert.Equal(DateTimeKind.Utc, valid.StartedAt.Kind);
        Assert.Equal("user-001", valid.WinnerUserId);
        Assert.Equal(JsonValueKind.Object, valid.Metadata.ValueKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MatchId_is_required(string? matchId)
    {
        var errors = ExpectErrors(ValidRequest() with { MatchId = matchId });

        Assert.Contains("matchId", errors.Keys);
    }

    [Fact]
    public void MatchId_has_a_maximum_length()
    {
        var errors = ExpectErrors(ValidRequest() with { MatchId = new string('x', ResultValidator.MaxIdLength + 1) });

        Assert.Contains("matchId", errors.Keys);
    }

    [Theory]
    [InlineData("trivia")]
    [InlineData("")]
    [InlineData(null)]
    public void GameType_must_be_typing(string? gameType)
    {
        var errors = ExpectErrors(ValidRequest() with { GameType = gameType });

        Assert.Contains("gameType", errors.Keys);
    }

    [Fact]
    public void Players_are_required()
    {
        Assert.Contains("players", ExpectErrors(ValidRequest() with { Players = null }).Keys);
        Assert.Contains("players", ExpectErrors(ValidRequest() with { Players = [] }).Keys);
    }

    [Fact]
    public void Player_fields_are_validated_and_reported_per_player()
    {
        var errors = ExpectErrors(ValidRequest() with { Players = [new(" ", "", -5)] });

        Assert.Contains("players[0].userId", errors.Keys);
        Assert.Contains("players[0].displayName", errors.Keys);
        Assert.Contains("players[0].score", errors.Keys);
    }

    [Fact]
    public void A_missing_score_is_rejected()
    {
        var errors = ExpectErrors(ValidRequest() with { Players = [new("user-001", "Ana", null)] });

        Assert.Contains("players[0].score", errors.Keys);
    }

    [Fact]
    public void A_null_player_is_rejected()
    {
        var errors = ExpectErrors(ValidRequest() with { Players = [new("user-001", "Ana", 1), null] });

        Assert.Contains("players[1]", errors.Keys);
    }

    [Fact]
    public void Duplicate_players_are_rejected()
    {
        var errors = ExpectErrors(ValidRequest() with { Players = [new("user-001", "Ana", 1), new("user-001", "Otra", 2)] });

        Assert.Contains("players[1].userId", errors.Keys);
    }

    [Fact]
    public void Dates_without_utc_are_rejected()
    {
        var unspecified = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);

        var errors = ExpectErrors(ValidRequest() with { StartedAt = unspecified, FinishedAt = unspecified });

        Assert.Contains("startedAt", errors.Keys);
        Assert.Contains("finishedAt", errors.Keys);
    }

    [Fact]
    public void Dates_are_required()
    {
        var errors = ExpectErrors(ValidRequest() with { StartedAt = null, FinishedAt = null });

        Assert.Contains("startedAt", errors.Keys);
        Assert.Contains("finishedAt", errors.Keys);
    }

    [Fact]
    public void Local_dates_are_converted_to_utc()
    {
        var local = DateTime.SpecifyKind(Start, DateTimeKind.Local);

        Assert.True(ResultValidator.TryValidate(ValidRequest() with { StartedAt = local, FinishedAt = local.AddMinutes(1) }, out var valid, out _));

        Assert.NotNull(valid);
        Assert.Equal(DateTimeKind.Utc, valid.StartedAt.Kind);
        Assert.Equal(local.ToUniversalTime(), valid.StartedAt);
    }

    [Fact]
    public void FinishedAt_cannot_be_before_StartedAt()
    {
        var errors = ExpectErrors(ValidRequest() with { FinishedAt = Start.AddSeconds(-1) });

        Assert.Contains("finishedAt", errors.Keys);
    }

    [Fact]
    public void The_winner_must_be_one_of_the_players()
    {
        var errors = ExpectErrors(ValidRequest() with { WinnerUserId = "intruso" });

        Assert.Contains("winnerUserId", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_winner_means_no_winner(string? winner)
    {
        Assert.True(ResultValidator.TryValidate(ValidRequest() with { WinnerUserId = winner }, out var valid, out _));

        Assert.NotNull(valid);
        Assert.Null(valid.WinnerUserId);
    }

    [Fact]
    public void Metadata_must_be_a_json_object()
    {
        var array = JsonDocument.Parse("[1, 2]").RootElement;

        var errors = ExpectErrors(ValidRequest() with { Metadata = array });

        Assert.Contains("metadata", errors.Keys);
    }

    [Fact]
    public void Metadata_has_a_maximum_size()
    {
        var huge = JsonDocument.Parse($"{{\"text\":\"{new string('a', ResultValidator.MaxMetadataChars)}\"}}").RootElement;

        var errors = ExpectErrors(ValidRequest() with { Metadata = huge });

        Assert.Contains("metadata", errors.Keys);
    }

    [Fact]
    public void Valid_metadata_is_kept()
    {
        var metadata = JsonDocument.Parse("""{"textId":"t-01","players":[{"userId":"user-001","wpm":60.5}]}""").RootElement;

        Assert.True(ResultValidator.TryValidate(ValidRequest() with { Metadata = metadata }, out var valid, out _));

        Assert.NotNull(valid);
        Assert.Equal("t-01", valid.Metadata.GetProperty("textId").GetString());
    }
}
