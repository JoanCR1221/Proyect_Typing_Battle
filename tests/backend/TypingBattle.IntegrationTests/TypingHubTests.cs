using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TypingBattle.Api.Game;
using TypingBattle.Api.Matchmaking;

namespace TypingBattle.IntegrationTests;

/// <summary>Partidas completas por el hub /hubs/typing, con SignalR real sobre el servidor de pruebas.</summary>
[Trait("Category", "Integration")]
public class TypingHubTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    private static string NewMatchId() => $"hub-{Guid.NewGuid():N}";

    private sealed class RecordingNotifier : IMatchmakingNotifier
    {
        public TaskCompletionSource<MatchFinishedNotification> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> NotifyMatchFinishedAsync(MatchFinishedNotification notification, CancellationToken cancellationToken = default)
        {
            Received.TrySetResult(notification);
            return Task.FromResult(true);
        }
    }

    /// <summary>Ana y Luis conectados, dentro de la sala, listos y con la carrera en marcha (ya tienen el texto).</summary>
    private static async Task<(HubPlayer Ana, HubPlayer Luis, string Text)> StartRaceAsync(WebApplicationFactory<Program> app, string matchId)
    {
        var ana = new HubPlayer(app, "ana");
        var luis = new HubPlayer(app, "luis");
        await ana.StartAsync();
        await luis.StartAsync();

        await ana.JoinAsync(matchId, "Ana");
        await luis.JoinAsync(matchId, "Luis");
        await ana.ReadyAsync();
        await luis.ReadyAsync();

        var started = await ana.Started.WaitAsync();
        await luis.Started.WaitAsync();
        return (ana, luis, started.Text);
    }

    [Fact]
    public async Task Two_players_play_a_full_match_and_the_result_is_saved_and_reported()
    {
        var notifier = new RecordingNotifier();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMatchmakingNotifier>();
            services.AddSingleton<IMatchmakingNotifier>(notifier);
        }));
        var matchId = NewMatchId();

        var (ana, luis, text) = await StartRaceAsync(app, matchId);
        await using var _ = ana;
        await using var __ = luis;

        // Ana escribe todo el texto primero: Luis se entera de que ella terminó.
        await ana.TypeAsync(text);
        var anaFinished = await luis.PlayerFinished.WaitAsync();
        Assert.Equal(("ana", 1), (anaFinished.UserId, anaFinished.Rank));
        Assert.Equal(100.0, anaFinished.Accuracy);

        // Luis va a la mitad, se equivoca, corrige y termina: la carrera acaba porque ya nadie falta.
        await luis.TypeAsync(text[..15]);
        await luis.TypeAsync(text[..15] + "#");
        await luis.TypeAsync(text);

        var end = await ana.Finished.WaitAsync();
        await luis.Finished.WaitAsync();
        Assert.Equal("ana", end.WinnerUserId);
        Assert.True(end.ResultSaved);
        Assert.Equal(["ana", "luis"], end.Standings.Select(s => s.UserId));
        Assert.Equal(["Ana", "Luis"], end.Standings.Select(s => s.DisplayName));
        Assert.All(end.Standings, s => Assert.True(s.Finished && s.Wpm > 0 && s.Score > 0));
        Assert.True(end.Standings[1].Accuracy < 100); // el error de Luis, aunque lo corrigió, cuenta

        // El resultado quedó guardado y se puede consultar por la API REST.
        var saved = await app.CreateClient().GetFromJsonAsync<JsonElement>($"/api/games/typing/results/{matchId}");
        Assert.Equal("ana", saved.GetProperty("winnerUserId").GetString());
        Assert.Equal(["ana", "luis"], saved.GetProperty("players").EnumerateArray().Select(p => p.GetProperty("userId").GetString()));
        var metadata = saved.GetProperty("metadata");
        Assert.Equal(text.Length, metadata.GetProperty("textLength").GetInt32());
        Assert.True(metadata.GetProperty("players")[0].GetProperty("wpm").GetDouble() > 0);

        // Y el historial del jugador ya lo incluye.
        var history = await app.CreateClient().GetFromJsonAsync<JsonElement>("/api/games/typing/players/ana/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("matchId").GetString() == matchId && h.GetProperty("won").GetBoolean());

        // Matchmaking recibió el aviso.
        var notification = await notifier.Received.Task.WaitAsync(Inbox<int>.DefaultTimeout);
        Assert.Equal((matchId, "typing", "ana"), (notification.MatchId, notification.GameType, notification.WinnerUserId));
    }

    [Fact]
    public async Task The_race_ends_by_time_limit_and_the_player_who_got_furthest_wins()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("Typing:TimeLimitSeconds", "2"));
        var matchId = NewMatchId();

        var (ana, luis, text) = await StartRaceAsync(app, matchId);
        await using var _ = ana;
        await using var __ = luis;
        await ana.TypeAsync(text[..25]);
        await luis.TypeAsync(text[..5]);

        var end = await ana.Finished.WaitAsync();

        Assert.Equal("ana", end.WinnerUserId);
        Assert.False(end.Standings[0].Finished);
        Assert.True(end.Standings[0].Progress > end.Standings[1].Progress);
        Assert.True(end.ResultSaved);
        var saved = await app.CreateClient().GetAsync($"/api/games/typing/results/{matchId}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task Progress_is_broadcast_to_every_player_while_racing()
    {
        var (ana, luis, text) = await StartRaceAsync(factory, NewMatchId());
        await using var _ = ana;
        await using var __ = luis;

        await ana.TypeAsync(text[..12]);

        var seenByLuis = await luis.Progress.WaitAsync(p => p.Players.Any(x => x.UserId == "ana" && x.Progress > 0));
        var anaState = seenByLuis.Players.Single(p => p.UserId == "ana");
        Assert.Equal(Math.Round(12 * 100.0 / text.Length, 1), anaState.Progress);
    }

    [Fact]
    public async Task Players_see_each_other_join_the_room()
    {
        var matchId = NewMatchId();
        await using var ana = new HubPlayer(factory, "ana");
        await using var luis = new HubPlayer(factory, "luis");
        await ana.StartAsync();
        await luis.StartAsync();

        var first = await ana.JoinAsync(matchId, "Ana");
        Assert.Equal((GameState.Waiting, 1), (first.State, first.Players.Count));

        await luis.JoinAsync(matchId, "Luis");

        var update = await ana.Rooms.WaitAsync(r => r.Players.Count == 2);
        Assert.Equal(["Ana", "Luis"], update.Players.Select(p => p.DisplayName));
    }

    [Fact]
    public async Task The_countdown_is_announced_when_everyone_is_ready()
    {
        var matchId = NewMatchId();
        await using var ana = new HubPlayer(factory, "ana");
        await using var luis = new HubPlayer(factory, "luis");
        await ana.StartAsync();
        await luis.StartAsync();
        await ana.JoinAsync(matchId, "Ana");
        await luis.JoinAsync(matchId, "Luis");

        await ana.ReadyAsync();
        await luis.ReadyAsync();

        var starting = await ana.Starting.WaitAsync();
        Assert.Equal(0, starting.CountdownSeconds); // la fábrica de pruebas acorta la cuenta regresiva
    }

    [Fact]
    public async Task A_third_player_cannot_join_a_race_in_progress()
    {
        var matchId = NewMatchId();
        var (ana, luis, _) = await StartRaceAsync(factory, matchId);
        await using var _ana = ana;
        await using var _luis = luis;
        await using var maria = new HubPlayer(factory, "maria");
        await maria.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(() => maria.JoinAsync(matchId, "María"));

        Assert.Contains("ya comenzó", error.Message);
    }

    [Fact]
    public async Task A_dropped_player_does_not_block_the_end_of_the_race()
    {
        var (ana, luis, text) = await StartRaceAsync(factory, NewMatchId());
        await using var _ana = ana;
        await using var _luis = luis;

        await luis.Connection.StopAsync();
        await ana.TypeAsync(text);

        var end = await ana.Finished.WaitAsync();
        Assert.Equal("ana", end.WinnerUserId);
        Assert.Equal(2, end.Standings.Count); // Luis sigue en la clasificación, con 0 % de avance
        Assert.Equal(0.0, end.Standings[1].Progress);
    }

    [Fact]
    public async Task Joining_validates_the_match_id()
    {
        await using var ana = new HubPlayer(factory, "ana");
        await ana.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(() => ana.JoinAsync("   ", "Ana"));

        Assert.Contains("matchId", error.Message);
    }

    [Fact]
    public async Task Setting_ready_before_joining_is_an_error()
    {
        await using var ana = new HubPlayer(factory, "ana");
        await ana.StartAsync();

        var error = await Assert.ThrowsAsync<HubException>(() => ana.ReadyAsync());

        Assert.Contains("JoinGame", error.Message);
    }

    [Fact]
    public async Task The_hub_rejects_connections_without_a_user()
    {
        await using var anonymous = new HubPlayer(factory);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => anonymous.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }
}
