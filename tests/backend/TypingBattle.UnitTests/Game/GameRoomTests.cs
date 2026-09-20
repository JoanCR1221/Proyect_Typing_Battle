using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TypingBattle.Api.Game;

namespace TypingBattle.UnitTests.Game;

[Trait("Category", "Unit")]
public class GameRoomTests
{
    private const string Text = "hola mundo"; // 10 caracteres: fácil de teclear en las pruebas

    private sealed class FixedTextProvider : ITextProvider
    {
        public TypingText Next() => new("t-test", Text);
    }

    /// <summary>Una sala con reloj falso: el tiempo solo avanza cuando la prueba lo dice.</summary>
    private sealed class Room
    {
        public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 2, 20, 0, 0, TimeSpan.Zero));
        public GameRoom Sut { get; }

        public Room(Action<TypingGameOptions>? configure = null)
        {
            var options = new TypingGameOptions
            {
                CountdownSeconds = 3,
                TimeLimitSeconds = 60,
                MinPlayers = 2,
                MaxPlayers = 3,
                RoomTtlSeconds = 60,
                MaxCharsPerSecond = 35,
                MaxBurstChars = 20,
            };
            configure?.Invoke(options);
            Sut = new GameRoom("match-1", options, new FixedTextProvider(), Time);
        }

        public DateTime Now => Time.GetUtcNow().UtcDateTime;

        public void JoinReady(string user)
        {
            Sut.Join(user, user, $"conn-{user}");
            Sut.SetReady(user);
        }

        /// <summary>Ana y Luis listos, cuenta regresiva completa y carrera en marcha.</summary>
        public void StartRace()
        {
            JoinReady("ana");
            JoinReady("luis");
            Time.Advance(TimeSpan.FromSeconds(3));
            Sut.Tick();
            Assert.Equal(GameState.Running, Sut.State);
        }
    }

    private static T Single<T>(IEnumerable<RoomEvent> events) where T : RoomEvent => Assert.Single(events.OfType<T>());

    // ---- sala de espera y cuenta regresiva ----

    [Fact]
    public void Joining_announces_the_room()
    {
        var room = new Room();

        var result = room.Sut.Join("ana", "Ana", "c1");

        Assert.True(result.Ok);
        var updated = Single<RoomUpdatedEvent>(result.Events);
        Assert.Equal(GameState.Waiting, updated.Snapshot.State);
        var player = Assert.Single(updated.Snapshot.Players);
        Assert.Equal(("ana", "Ana", false), (player.UserId, player.DisplayName, player.Ready));
    }

    [Fact]
    public void Rejoining_reuses_the_player_and_updates_the_name()
    {
        var room = new Room();
        room.Sut.Join("ana", "Ana", "c1");

        var result = room.Sut.Join("ana", "Ana María", "c2");

        var players = Single<RoomUpdatedEvent>(result.Events).Snapshot.Players;
        Assert.Equal("Ana María", Assert.Single(players).DisplayName);
    }

    [Fact]
    public void A_full_room_rejects_newcomers()
    {
        var room = new Room(o => o.MaxPlayers = 2);
        room.Sut.Join("ana", "Ana", "c1");
        room.Sut.Join("luis", "Luis", "c2");

        var result = room.Sut.Join("maria", "María", "c3");

        Assert.False(result.Ok);
        Assert.Contains("llena", result.Error);
    }

    [Fact]
    public void Setting_ready_requires_being_in_the_room()
    {
        var result = new Room().Sut.SetReady("fantasma");

        Assert.False(result.Ok);
    }

    [Fact]
    public void The_countdown_starts_only_when_enough_players_are_all_ready()
    {
        var room = new Room();

        var alone = room.Sut.Join("ana", "Ana", "c1");
        room.Sut.SetReady("ana");
        Assert.Equal(GameState.Waiting, room.Sut.State); // 1 jugador < MinPlayers
        Assert.Empty(alone.Events.OfType<GameStartingEvent>());

        room.Sut.Join("luis", "Luis", "c2");
        Assert.Equal(GameState.Waiting, room.Sut.State); // Luis todavía no está listo

        var ready = room.Sut.SetReady("luis");

        Assert.Equal(GameState.Countdown, room.Sut.State);
        var starting = Single<GameStartingEvent>(ready.Events).Payload;
        Assert.Equal(3, starting.CountdownSeconds);
        Assert.Equal(room.Now.AddSeconds(3), starting.StartsAt);
    }

    [Fact]
    public void Leaving_during_the_countdown_cancels_it()
    {
        var room = new Room();
        room.JoinReady("ana");
        room.JoinReady("luis");
        Assert.Equal(GameState.Countdown, room.Sut.State);

        var result = room.Sut.Leave("luis");

        Assert.Equal(GameState.Waiting, room.Sut.State);
        var players = Single<RoomUpdatedEvent>(result.Events).Snapshot.Players;
        Assert.Equal("ana", Assert.Single(players).UserId);
    }

    [Fact]
    public void Un_readying_during_the_countdown_cancels_it()
    {
        var room = new Room();
        room.JoinReady("ana");
        room.JoinReady("luis");

        room.Sut.SetReady("luis", ready: false);

        Assert.Equal(GameState.Waiting, room.Sut.State);
    }

    [Fact]
    public void The_race_starts_when_the_countdown_ends()
    {
        var room = new Room();
        room.JoinReady("ana");
        room.JoinReady("luis");

        room.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(room.Sut.Tick()); // todavía falta 1 s
        Assert.Equal(GameState.Countdown, room.Sut.State);

        room.Time.Advance(TimeSpan.FromSeconds(1));
        var started = Single<GameStartedEvent>(room.Sut.Tick()).Payload;

        Assert.Equal(GameState.Running, room.Sut.State);
        Assert.Equal(("t-test", Text, 60), (started.TextId, started.Text, started.TimeLimitSeconds));
        Assert.Equal(started.StartedAt.AddSeconds(60), started.EndsAt);
        Assert.Equal(DateTimeKind.Utc, started.StartedAt.Kind);
    }

    [Fact]
    public void The_text_is_hidden_until_the_race_starts()
    {
        var room = new Room();
        room.JoinReady("ana");
        room.JoinReady("luis");

        Assert.Null(room.Sut.Snapshot("ana").Text);

        room.Time.Advance(TimeSpan.FromSeconds(3));
        room.Sut.Tick();

        Assert.Equal(Text, room.Sut.Snapshot("ana").Text);
    }

    // ---- carrera ----

    [Fact]
    public void Newcomers_cannot_join_a_race_in_progress()
    {
        var room = new Room();
        room.StartRace();

        var result = room.Sut.Join("maria", "María", "c9");

        Assert.False(result.Ok);
        Assert.Contains("comenzó", result.Error);
    }

    [Fact]
    public void A_player_who_dropped_can_reconnect_and_recover_their_progress()
    {
        var room = new Room();
        room.StartRace();
        room.Sut.SubmitProgress("ana", "hol");
        room.Sut.Disconnect("ana", "conn-ana");

        var result = room.Sut.Join("ana", "Ana", "conn-nueva");

        Assert.True(result.Ok);
        var snapshot = room.Sut.Snapshot("ana");
        Assert.Equal(("hol", Text), (snapshot.MyTyped, snapshot.Text));
        Assert.True(snapshot.Players.Single(p => p.UserId == "ana").Connected);
    }

    [Fact]
    public void Progress_outside_the_race_is_ignored()
    {
        var room = new Room();
        room.JoinReady("ana");

        var result = room.Sut.SubmitProgress("ana", "hola");

        Assert.True(result.Ok);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Progress_is_broadcast_on_the_next_tick_and_only_when_it_changed()
    {
        var room = new Room();
        room.StartRace();
        room.Time.Advance(TimeSpan.FromSeconds(1));

        room.Sut.SubmitProgress("ana", "ho");
        var first = Single<ProgressUpdatedEvent>(room.Sut.Tick()).Payload;

        var ana = first.Players.Single(p => p.UserId == "ana");
        Assert.Equal(20.0, ana.Progress);
        Assert.Equal(100.0, ana.Accuracy);
        Assert.Empty(room.Sut.Tick()); // sin cambios, no se repite
    }

    [Fact]
    public void The_first_to_finish_wins_and_the_race_ends_when_everyone_has_finished()
    {
        var room = new Room();
        room.StartRace();

        room.Time.Advance(TimeSpan.FromSeconds(10));
        var anaDone = room.Sut.SubmitProgress("ana", Text);

        var finished = Single<PlayerFinishedEvent>(anaDone.Events).Payload;
        Assert.Equal(("ana", 1, 12.0, 100.0), (finished.UserId, finished.Rank, finished.Wpm, finished.Accuracy));
        Assert.Empty(anaDone.Events.OfType<GameFinishedEvent>()); // Luis sigue escribiendo
        Assert.Equal(GameState.Running, room.Sut.State);

        room.Time.Advance(TimeSpan.FromSeconds(5));
        var luisDone = room.Sut.SubmitProgress("luis", Text);

        var end = Single<GameFinishedEvent>(luisDone.Events);
        Assert.Equal(GameState.Finished, room.Sut.State);
        Assert.Equal("ana", end.Payload.WinnerUserId);
        Assert.Equal(["ana", "luis"], end.Payload.Standings.Select(s => s.UserId));
        Assert.Equal([12.0, 8.0], end.Payload.Standings.Select(s => s.Wpm)); // 10 s y 15 s para 10 caracteres
        Assert.Equal([120, 80], end.Payload.Standings.Select(s => s.Score));
        Assert.False(end.Payload.ResultSaved); // lo decide quien lo guarda, no la sala
    }

    [Fact]
    public void The_result_to_save_follows_the_final_ranking_and_carries_typing_metadata()
    {
        var room = new Room();
        room.StartRace();
        room.Time.Advance(TimeSpan.FromSeconds(10));
        room.Sut.SubmitProgress("ana", Text);
        room.Time.Advance(TimeSpan.FromSeconds(5));
        var end = Single<GameFinishedEvent>(room.Sut.SubmitProgress("luis", Text).Events);

        var result = end.Result;

        Assert.NotNull(result);
        Assert.Equal(("match-1", "typing", "ana"), (result.MatchId, result.GameType, result.WinnerUserId));
        Assert.Equal(["ana", "luis"], result.Players!.Select(p => p!.UserId));
        Assert.Equal([120, 80], result.Players!.Select(p => p!.Score));
        Assert.Equal(DateTimeKind.Utc, result.StartedAt!.Value.Kind);

        var metadata = result.Metadata!.Value;
        Assert.Equal("t-test", metadata.GetProperty("textId").GetString());
        Assert.Equal(10, metadata.GetProperty("textLength").GetInt32());
        var first = metadata.GetProperty("players")[0];
        Assert.Equal("ana", first.GetProperty("userId").GetString());
        Assert.Equal(12.0, first.GetProperty("wpm").GetDouble());
        Assert.Equal(100.0, first.GetProperty("accuracy").GetDouble());
        Assert.True(first.GetProperty("finished").GetBoolean());
        Assert.Equal(10.0, first.GetProperty("finishSeconds").GetDouble());
    }

    [Fact]
    public void The_time_limit_ends_the_race_and_the_player_who_got_furthest_wins()
    {
        var room = new Room();
        room.StartRace();
        room.Sut.SubmitProgress("ana", "hol");

        room.Time.Advance(TimeSpan.FromSeconds(59));
        Assert.Empty(room.Sut.Tick().OfType<GameFinishedEvent>());

        room.Time.Advance(TimeSpan.FromSeconds(1));
        var end = Single<GameFinishedEvent>(room.Sut.Tick());

        Assert.Equal("ana", end.Payload.WinnerUserId);
        Assert.Equal(["ana", "luis"], end.Payload.Standings.Select(s => s.UserId));
        Assert.False(end.Payload.Standings[0].Finished);
        Assert.NotNull(end.Result);
    }

    [Fact]
    public void Standings_put_finishers_first_and_then_order_the_rest_by_progress()
    {
        var room = new Room(o => { o.MinPlayers = 3; o.MaxPlayers = 3; });
        foreach (var user in new[] { "ana", "luis", "maria" })
        {
            room.JoinReady(user);
        }

        room.Time.Advance(TimeSpan.FromSeconds(3));
        room.Sut.Tick();
        room.Sut.SubmitProgress("maria", "ho");
        room.Sut.SubmitProgress("luis", "hola m");
        room.Time.Advance(TimeSpan.FromSeconds(5));
        room.Sut.SubmitProgress("ana", Text);

        room.Time.Advance(TimeSpan.FromSeconds(60));
        var end = Single<GameFinishedEvent>(room.Sut.Tick());

        Assert.Equal(["ana", "luis", "maria"], end.Payload.Standings.Select(s => s.UserId));
        Assert.Equal([1, 2, 3], end.Payload.Standings.Select(s => s.Rank));
    }

    [Fact]
    public void When_nobody_types_there_is_no_winner_and_nothing_to_save()
    {
        var room = new Room();
        room.StartRace();

        room.Time.Advance(TimeSpan.FromSeconds(60));
        var end = Single<GameFinishedEvent>(room.Sut.Tick());

        Assert.Null(end.Payload.WinnerUserId);
        Assert.Null(end.Result);
    }

    [Fact]
    public void A_disconnected_player_does_not_hold_back_the_end_of_the_race()
    {
        var room = new Room();
        room.StartRace();
        room.Sut.Disconnect("luis", "conn-luis");

        var result = room.Sut.SubmitProgress("ana", Text);

        var end = Single<GameFinishedEvent>(result.Events);
        Assert.Equal("ana", end.Payload.WinnerUserId);
        Assert.Equal(["ana", "luis"], end.Payload.Standings.Select(s => s.UserId)); // Luis sigue en la clasificación
    }

    [Fact]
    public void If_everyone_disconnects_the_race_ends_without_a_winner()
    {
        var room = new Room();
        room.StartRace();
        room.Sut.Disconnect("ana", "conn-ana");
        room.Sut.Disconnect("luis", "conn-luis");

        var end = Single<GameFinishedEvent>(room.Sut.Tick());

        Assert.Null(end.Payload.WinnerUserId);
        Assert.Null(end.Result);
    }

    [Fact]
    public void Progress_faster_than_a_human_can_type_is_ignored()
    {
        var room = new Room(o => { o.MaxCharsPerSecond = 3; o.MaxBurstChars = 2; });
        room.StartRace();
        room.Time.Advance(TimeSpan.FromSeconds(1)); // a 1 s se permiten 3 * 1 + 2 = 5 caracteres

        var pasted = room.Sut.SubmitProgress("ana", Text); // los 10 de golpe: imposible
        Assert.Empty(pasted.Events);
        Assert.Empty(room.Sut.Tick().OfType<PlayerFinishedEvent>());
        Assert.Equal(0.0, room.Sut.Snapshot().Players.Single(p => p.UserId == "ana").Progress);

        room.Sut.SubmitProgress("ana", "hola "); // 5 caracteres: dentro del límite
        var progress = Single<ProgressUpdatedEvent>(room.Sut.Tick()).Payload;
        Assert.Equal(50.0, progress.Players.Single(p => p.UserId == "ana").Progress);
    }

    [Fact]
    public void A_late_submission_after_the_time_limit_is_ignored()
    {
        var room = new Room();
        room.StartRace();
        room.Time.Advance(TimeSpan.FromSeconds(61));

        var result = room.Sut.SubmitProgress("ana", Text);

        Assert.Empty(result.Events);
    }

    // ---- limpieza ----

    [Fact]
    public void A_finished_room_expires_after_the_ttl()
    {
        var room = new Room();
        room.StartRace();
        room.Time.Advance(TimeSpan.FromSeconds(60));
        room.Sut.Tick();

        Assert.False(room.Sut.IsExpired());

        room.Time.Advance(TimeSpan.FromSeconds(60));
        Assert.True(room.Sut.IsExpired());
    }

    [Fact]
    public void An_empty_waiting_room_expires_after_the_ttl()
    {
        var room = new Room();
        room.Sut.Join("ana", "Ana", "c1");
        room.Sut.Disconnect("ana", "c1");

        Assert.False(room.Sut.IsExpired());

        room.Time.Advance(TimeSpan.FromSeconds(60));
        Assert.True(room.Sut.IsExpired());
    }

    [Fact]
    public void A_room_in_progress_never_expires()
    {
        var room = new Room();
        room.StartRace();
        room.Time.Advance(TimeSpan.FromSeconds(30));

        Assert.False(room.Sut.IsExpired());
    }
}
