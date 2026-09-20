using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Flujos de la API de resultados exigidos por 04-persistencia-y-api-juegos.md:
/// <c>POST /results → GET /results/{matchId}</c> e historial con más de un resultado.
/// </summary>
[Trait("Category", "Integration")]
public class ResultsApiTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    private const string Base = "/api/games/typing";
    private static readonly DateTime T0 = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);

    private readonly HttpClient _client = factory.CreateClient();

    private sealed record Player(string UserId, string Name, int Score, double Wpm, double Accuracy);

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static object NewResult(string matchId, DateTime finishedAt, string? winner, params Player[] players) => new
    {
        matchId,
        gameType = "typing",
        players = players.Select(p => new { userId = p.UserId, displayName = p.Name, score = p.Score }),
        startedAt = finishedAt.AddSeconds(-60),
        finishedAt,
        winnerUserId = winner,
        metadata = new
        {
            textId = "t-01",
            players = players.Select(p => new { userId = p.UserId, wpm = p.Wpm, accuracy = p.Accuracy }),
        },
    };

    private async Task<HttpResponseMessage> PostAsync(object body) => await _client.PostAsJsonAsync($"{Base}/results", body);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Post_then_get_returns_the_saved_result()
    {
        var matchId = NewId("match");
        var ana = new Player(NewId("ana"), "Ana", 850, 62.4, 96.1);
        var luis = new Player(NewId("luis"), "Luis", 620, 44.0, 91.5);

        var post = await PostAsync(NewResult(matchId, T0, ana.UserId, ana, luis));

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal($"{Base}/results/{matchId}", post.Headers.Location?.OriginalString);

        var get = await _client.GetAsync($"{Base}/results/{matchId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var result = await ReadAsync(get);

        Assert.Equal(matchId, result.GetProperty("matchId").GetString());
        Assert.Equal("typing", result.GetProperty("gameType").GetString());
        Assert.Equal(ana.UserId, result.GetProperty("winnerUserId").GetString());
        Assert.Equal("2026-09-02T19:59:00Z", result.GetProperty("startedAt").GetString());
        Assert.Equal("2026-09-02T20:00:00Z", result.GetProperty("finishedAt").GetString());

        var players = result.GetProperty("players").EnumerateArray().ToList();
        Assert.Equal([ana.UserId, luis.UserId], players.Select(p => p.GetProperty("userId").GetString()));
        Assert.Equal(850, players[0].GetProperty("score").GetInt32());
        Assert.Equal("Luis", players[1].GetProperty("displayName").GetString());

        Assert.Equal("t-01", result.GetProperty("metadata").GetProperty("textId").GetString());
    }

    [Fact]
    public async Task Dates_with_an_offset_are_stored_as_utc()
    {
        var matchId = NewId("match");
        var json = $$"""
            { "matchId": "{{matchId}}", "gameType": "typing",
              "players": [ { "userId": "u1", "displayName": "Ana", "score": 1 } ],
              "startedAt": "2026-09-02T14:00:00-06:00", "finishedAt": "2026-09-02T14:01:00-06:00",
              "winnerUserId": "u1" }
            """;

        var post = await _client.PostAsync($"{Base}/results", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var result = await ReadAsync(await _client.GetAsync($"{Base}/results/{matchId}"));
        Assert.Equal("2026-09-02T20:00:00Z", result.GetProperty("startedAt").GetString());
        Assert.Equal("2026-09-02T20:01:00Z", result.GetProperty("finishedAt").GetString());
        Assert.Equal(JsonValueKind.Object, result.GetProperty("metadata").ValueKind);
    }

    [Fact]
    public async Task A_match_can_only_be_recorded_once()
    {
        var matchId = NewId("match");
        var ana = new Player(NewId("ana"), "Ana", 500, 50, 95);

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(NewResult(matchId, T0, ana.UserId, ana))).StatusCode);
        var duplicate = await PostAsync(NewResult(matchId, T0, ana.UserId, ana));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task An_invalid_body_returns_400_with_the_offending_fields()
    {
        var post = await _client.PostAsync($"{Base}/results", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        var errors = (await ReadAsync(post)).GetProperty("errors");
        foreach (var field in new[] { "matchId", "gameType", "players", "startedAt", "finishedAt" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Falta el error de '{field}'");
        }
    }

    [Fact]
    public async Task A_date_without_utc_is_rejected()
    {
        var json = """
            { "matchId": "m-sin-zona", "gameType": "typing",
              "players": [ { "userId": "u1", "displayName": "Ana", "score": 1 } ],
              "startedAt": "2026-09-02T20:00:00", "finishedAt": "2026-09-02T20:01:00Z" }
            """;

        var post = await _client.PostAsync($"{Base}/results", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        Assert.True((await ReadAsync(post)).GetProperty("errors").TryGetProperty("startedAt", out _));
    }

    [Fact]
    public async Task An_unknown_match_returns_404()
    {
        var get = await _client.GetAsync($"{Base}/results/{NewId("no-existe")}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task History_lists_every_match_of_the_player_newest_first()
    {
        var me = new Player(NewId("yo"), "Yo", 600, 60, 100);
        var rival = new Player(NewId("rival"), "Rival", 500, 50, 90);
        var matches = new[] { NewId("h1"), NewId("h2"), NewId("h3") };

        // Se guardan desordenados a propósito: el historial debe ordenar por fecha de fin, no por inserción.
        await PostAsync(NewResult(matches[1], T0.AddHours(1), rival.UserId, rival, me with { Score = 300, Wpm = 30, Accuracy = 90 }));
        await PostAsync(NewResult(matches[2], T0.AddHours(2), me.UserId, me, rival));
        await PostAsync(NewResult(matches[0], T0, rival.UserId, rival, me with { Score = 450, Wpm = 45, Accuracy = 95 }));

        var history = (await ReadAsync(await _client.GetAsync($"{Base}/players/{me.UserId}/history"))).EnumerateArray().ToList();

        Assert.Equal([matches[2], matches[1], matches[0]], history.Select(h => h.GetProperty("matchId").GetString()));
        Assert.Equal([true, false, false], history.Select(h => h.GetProperty("won").GetBoolean()));
        Assert.Equal([1, 2, 2], history.Select(h => h.GetProperty("position").GetInt32()));
        Assert.All(history, h => Assert.Equal(2, h.GetProperty("playersCount").GetInt32()));
        Assert.Equal([600, 300, 450], history.Select(h => h.GetProperty("score").GetInt32()));
        Assert.Equal(30, history[1].GetProperty("wpm").GetDouble());
        Assert.Equal(95, history[2].GetProperty("accuracy").GetDouble());
        Assert.Equal("2026-09-02T22:00:00Z", history[0].GetProperty("finishedAt").GetString());
    }

    [Fact]
    public async Task History_supports_limit_and_offset()
    {
        var me = new Player(NewId("yo"), "Yo", 100, 10, 90);
        var matches = Enumerable.Range(0, 3).Select(i => NewId($"p{i}")).ToArray();
        for (var i = 0; i < matches.Length; i++)
        {
            await PostAsync(NewResult(matches[i], T0.AddHours(i), me.UserId, me));
        }

        var page = await ReadAsync(await _client.GetAsync($"{Base}/players/{me.UserId}/history?limit=1&offset=1"));

        var only = Assert.Single(page.EnumerateArray());
        Assert.Equal(matches[1], only.GetProperty("matchId").GetString());
    }

    [Fact]
    public async Task History_of_an_unknown_player_is_an_empty_list()
    {
        var response = await _client.GetAsync($"{Base}/players/{NewId("nadie")}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadAsync(response)).EnumerateArray());
    }

    [Fact]
    public async Task Player_ids_with_special_characters_work_when_url_encoded()
    {
        // Los ids de Auth0 tienen la forma "auth0|64f0c1..." (con una barra vertical).
        var auth0Id = $"auth0|{Guid.NewGuid():N}";
        var me = new Player(auth0Id, "Usuario Auth0", 700, 70, 99);
        await PostAsync(NewResult(NewId("a0"), T0, auth0Id, me));

        var history = await ReadAsync(await _client.GetAsync($"{Base}/players/{Uri.EscapeDataString(auth0Id)}/history"));
        var stats = await ReadAsync(await _client.GetAsync($"{Base}/players/{Uri.EscapeDataString(auth0Id)}/stats"));

        Assert.Single(history.EnumerateArray());
        Assert.Equal(auth0Id, stats.GetProperty("userId").GetString());
        Assert.Equal(1, stats.GetProperty("gamesPlayed").GetInt32());
    }

    [Fact]
    public async Task Stats_aggregate_all_the_matches_of_the_player()
    {
        var me = new Player(NewId("yo"), "Yo", 0, 0, 0);
        var rival = new Player(NewId("rival"), "Rival", 100, 10, 80);

        await PostAsync(NewResult(NewId("s1"), T0, me.UserId, me with { Score = 600, Wpm = 60, Accuracy = 100 }, rival));
        await PostAsync(NewResult(NewId("s2"), T0.AddHours(1), rival.UserId, rival, me with { Score = 300, Wpm = 30, Accuracy = 90 }));
        await PostAsync(NewResult(NewId("s3"), T0.AddHours(2), rival.UserId, rival, me with { Score = 450, Wpm = 45, Accuracy = 95 }));

        var stats = await ReadAsync(await _client.GetAsync($"{Base}/players/{me.UserId}/stats"));

        Assert.Equal(3, stats.GetProperty("gamesPlayed").GetInt32());
        Assert.Equal(1, stats.GetProperty("wins").GetInt32());
        Assert.Equal(0.333, stats.GetProperty("winRate").GetDouble(), 3);
        Assert.Equal(450, stats.GetProperty("averageScore").GetDouble());
        Assert.Equal(600, stats.GetProperty("bestScore").GetInt32());
        Assert.Equal(45, stats.GetProperty("averageWpm").GetDouble());
        Assert.Equal(60, stats.GetProperty("bestWpm").GetDouble());
        Assert.Equal(95, stats.GetProperty("averageAccuracy").GetDouble());
        Assert.Equal("2026-09-02T22:00:00Z", stats.GetProperty("lastPlayedAt").GetString());
    }

    [Fact]
    public async Task Stats_of_a_player_without_matches_are_zeros()
    {
        var stats = await ReadAsync(await _client.GetAsync($"{Base}/players/{NewId("nuevo")}/stats"));

        Assert.Equal(0, stats.GetProperty("gamesPlayed").GetInt32());
        Assert.Equal(0, stats.GetProperty("wins").GetInt32());
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("averageWpm").ValueKind);
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("lastPlayedAt").ValueKind);
    }

    [Fact]
    public async Task Health_endpoint_responds()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
