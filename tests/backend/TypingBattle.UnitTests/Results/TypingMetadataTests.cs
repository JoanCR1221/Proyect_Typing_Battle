using System.Text.Json;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Results;

[Trait("Category", "Unit")]
public class TypingMetadataTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Reads_wpm_and_accuracy_per_player()
    {
        var metadata = Json("""
            { "textId": "t-01", "players": [
                { "userId": "user-001", "wpm": 62.4, "accuracy": 96.1 },
                { "userId": "user-002", "wpm": 40, "accuracy": 88 } ] }
            """);

        var stats = TypingMetadata.ExtractPlayerStats(metadata);

        Assert.Equal(2, stats.Count);
        Assert.Equal(new PlayerTypingStats(62.4, 96.1), stats["user-001"]);
        Assert.Equal(new PlayerTypingStats(40, 88), stats["user-002"]);
    }

    [Fact]
    public void Returns_empty_when_there_are_no_players()
    {
        Assert.Empty(TypingMetadata.ExtractPlayerStats(Json("{}")));
        Assert.Empty(TypingMetadata.ExtractPlayerStats(Json("""{"players":"nope"}""")));
        Assert.Empty(TypingMetadata.ExtractPlayerStats(Json("[]")));
    }

    [Fact]
    public void Ignores_malformed_entries()
    {
        var metadata = Json("""
            { "players": [ 7, {"wpm": 50}, {"userId": 3, "wpm": 50}, {"userId": " "}, {"userId": "ok", "wpm": "rapido"} ] }
            """);

        var stats = TypingMetadata.ExtractPlayerStats(metadata);

        var only = Assert.Single(stats);
        Assert.Equal("ok", only.Key);
        Assert.Equal(new PlayerTypingStats(null, null), only.Value);
    }
}
