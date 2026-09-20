using System.Text.Json;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Game;

/// <summary>
/// Una partida de Typing Battle: sala de espera, cuenta regresiva, carrera y clasificación final.
/// Es una máquina de estados sin SignalR ni base de datos: todo el tiempo sale de un <see cref="TimeProvider"/>
/// (así se prueba con un reloj falso) y cada operación devuelve los <see cref="RoomEvent"/> que hay que difundir.
/// Es segura entre hilos: el hub y el ciclo de fondo la usan a la vez.
/// </summary>
public sealed class GameRoom
{
    private static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private readonly TypingGameOptions _options;
    private readonly ITextProvider _texts;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, PlayerProgress> _players = new(StringComparer.Ordinal);

    private int _joinCounter;
    private int _finishCounter;
    private bool _progressDirty;
    private DateTime _lastActivity;
    private DateTime? _startsAt;
    private DateTime? _startedAt;
    private DateTime? _endsAt;
    private DateTime? _finishedAt;
    private TypingText? _text;

    public GameRoom(string matchId, TypingGameOptions options, ITextProvider texts, TimeProvider time)
    {
        MatchId = matchId;
        _options = options;
        _texts = texts;
        _time = time;
        _lastActivity = Now;
    }

    public string MatchId { get; }

    public GameState State { get; private set; } = GameState.Waiting;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;
    private int CountdownSeconds => Math.Max(0, _options.CountdownSeconds);
    private int TimeLimitSeconds => Math.Max(1, _options.TimeLimitSeconds);
    private int MinPlayers => Math.Max(1, _options.MinPlayers);
    private int MaxPlayers => Math.Max(MinPlayers, _options.MaxPlayers);

    /// <summary>
    /// Une al jugador a la sala (o lo reconecta). Solo se puede entrar nuevo mientras se espera; quien ya estaba
    /// puede volver en cualquier momento, incluso a mitad de carrera.
    /// </summary>
    public RoomResult Join(string userId, string displayName, string connectionId)
    {
        lock (_gate)
        {
            var now = Now;
            _lastActivity = now;

            if (!_players.TryGetValue(userId, out var player))
            {
                if (State == GameState.Finished)
                {
                    return RoomResult.Fail("La partida ya terminó.");
                }

                if (State != GameState.Waiting)
                {
                    return RoomResult.Fail("La partida ya comenzó: no se puede entrar a mitad de carrera.");
                }

                if (_players.Count >= MaxPlayers)
                {
                    return RoomResult.Fail("La sala está llena.");
                }

                player = new PlayerProgress(userId, displayName, ++_joinCounter);
                _players.Add(userId, player);
            }
            else if (!string.IsNullOrWhiteSpace(displayName))
            {
                player.DisplayName = displayName;
            }

            player.Connections.Add(connectionId);
            _progressDirty = true;
            return RoomResult.Success(WithLobbyEvents(now));
        }
    }

    /// <summary>Marca (o desmarca) al jugador como listo. Cuando todos los conectados están listos empieza la cuenta regresiva.</summary>
    public RoomResult SetReady(string userId, bool ready = true)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(userId, out var player))
            {
                return RoomResult.Fail("No estás en esta sala.");
            }

            if (State is GameState.Running or GameState.Finished)
            {
                return RoomResult.Success();
            }

            var now = Now;
            _lastActivity = now;
            player.Ready = ready;
            return RoomResult.Success(WithLobbyEvents(now));
        }
    }

    /// <summary>Cierra una conexión del jugador. Si era la última, en la sala de espera sale de la sala y en carrera queda desconectado.</summary>
    public RoomResult Disconnect(string userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(userId, out var player))
            {
                return RoomResult.Success();
            }

            player.Connections.Remove(connectionId);
            return player.Connected ? RoomResult.Success() : PlayerGone(player);
        }
    }

    /// <summary>El jugador abandona la sala con todas sus conexiones.</summary>
    public RoomResult Leave(string userId)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(userId, out var player))
            {
                return RoomResult.Success();
            }

            player.Connections.Clear();
            return PlayerGone(player);
        }
    }

    /// <summary>
    /// Recibe lo que el jugador tiene escrito. Se ignora, sin error, si la carrera no está en curso o si el avance es
    /// más rápido de lo humanamente posible (ver <see cref="TypingGameOptions.MaxCharsPerSecond"/>).
    /// </summary>
    public RoomResult SubmitProgress(string userId, string? typed)
    {
        lock (_gate)
        {
            if (State != GameState.Running || _text is null || !_players.TryGetValue(userId, out var player))
            {
                return RoomResult.Success();
            }

            var now = Now;
            if (now >= _endsAt)
            {
                return RoomResult.Success();
            }

            typed ??= "";
            var elapsed = now - _startedAt!.Value;
            var allowed = (long)(Math.Max(elapsed.TotalSeconds, 0) * _options.MaxCharsPerSecond) + _options.MaxBurstChars;
            if (typed.Length > allowed || !player.Apply(_text.Text, typed, now))
            {
                return RoomResult.Success();
            }

            _progressDirty = true;
            var events = new List<RoomEvent>();
            if (player.Finished)
            {
                player.FinishRank = ++_finishCounter;
                events.Add(new PlayerFinishedEvent(new PlayerFinishedDto(
                    userId,
                    player.FinishRank.Value,
                    TypingScorer.Wpm(player.CorrectChars, player.FinishedAt!.Value - _startedAt.Value),
                    TypingScorer.Accuracy(player.Keystrokes, player.Errors))));

                if (AllActivePlayersDone())
                {
                    FinishGame(now, events);
                }
            }

            return RoomResult.Success(events);
        }
    }

    /// <summary>
    /// Avanza la sala con el paso del tiempo: empieza la carrera al terminar la cuenta regresiva, la cierra por
    /// tiempo o porque ya nadie queda por terminar, y difunde el progreso si cambió desde la última vez.
    /// </summary>
    public IReadOnlyList<RoomEvent> Tick()
    {
        lock (_gate)
        {
            var now = Now;
            var events = new List<RoomEvent>();

            if (State == GameState.Countdown && now >= _startsAt)
            {
                StartGame(now, events);
            }

            if (State == GameState.Running)
            {
                if (now >= _endsAt || AllActivePlayersDone())
                {
                    FinishGame(now, events);
                }
                else if (_progressDirty)
                {
                    _progressDirty = false;
                    events.Add(new ProgressUpdatedEvent(new ProgressDto(now, PlayerStates(now))));
                }
            }

            return events;
        }
    }

    /// <summary>Foto de la sala. Con <paramref name="forUserId"/> incluye además lo que ese jugador lleva escrito.</summary>
    public RoomSnapshotDto Snapshot(string? forUserId = null)
    {
        lock (_gate)
        {
            return BuildSnapshot(forUserId, Now);
        }
    }

    /// <summary>Una sala terminada, o vacía desde hace rato, ya no hace falta en memoria.</summary>
    public bool IsExpired()
    {
        lock (_gate)
        {
            var ttl = TimeSpan.FromSeconds(Math.Max(1, _options.RoomTtlSeconds));
            var now = Now;
            return State switch
            {
                GameState.Finished => now - _finishedAt >= ttl,
                GameState.Waiting => !_players.Values.Any(p => p.Connected) && now - _lastActivity >= ttl,
                _ => false,
            };
        }
    }

    private RoomResult PlayerGone(PlayerProgress player)
    {
        var now = Now;
        _lastActivity = now;

        if (State is GameState.Waiting or GameState.Countdown)
        {
            _players.Remove(player.UserId);
        }
        else
        {
            _progressDirty = true;
        }

        return RoomResult.Success(WithLobbyEvents(now));
    }

    /// <summary>Anuncia el estado de la sala y, si corresponde, el inicio (o la cancelación) de la cuenta regresiva.</summary>
    private List<RoomEvent> WithLobbyEvents(DateTime now)
    {
        var starting = EvaluateLobby(now);
        var events = new List<RoomEvent> { new RoomUpdatedEvent(BuildSnapshot(null, now)) };
        if (starting is not null)
        {
            events.Add(starting);
        }

        return events;
    }

    private GameStartingEvent? EvaluateLobby(DateTime now)
    {
        if (State is not (GameState.Waiting or GameState.Countdown))
        {
            return null;
        }

        var connected = _players.Values.Where(p => p.Connected).ToList();
        var allReady = connected.Count >= MinPlayers && connected.All(p => p.Ready);

        if (State == GameState.Waiting && allReady)
        {
            State = GameState.Countdown;
            _startsAt = now.AddSeconds(CountdownSeconds);
            return new GameStartingEvent(new GameStartingDto(now, _startsAt.Value, CountdownSeconds));
        }

        if (State == GameState.Countdown && !allReady)
        {
            State = GameState.Waiting;
            _startsAt = null;
        }

        return null;
    }

    private void StartGame(DateTime now, List<RoomEvent> events)
    {
        State = GameState.Running;
        _text = _texts.Next();
        _startedAt = _startsAt ?? now;
        _endsAt = _startedAt.Value.AddSeconds(TimeLimitSeconds);
        _progressDirty = true;

        events.Add(new GameStartedEvent(new GameStartedDto(
            now, _startedAt.Value, _endsAt.Value, TimeLimitSeconds, _text.Id, _text.Text)));
    }

    /// <summary>La carrera termina cuando todos los jugadores conectados ya terminaron (o si no queda nadie conectado).</summary>
    private bool AllActivePlayersDone() => _players.Values.Where(p => p.Connected).All(p => p.Finished);

    private void FinishGame(DateTime now, List<RoomEvent> events)
    {
        State = GameState.Finished;
        _finishedAt = now;

        var standings = BuildStandings(now);
        var winner = standings.Count > 0 && (standings[0].Finished || standings[0].Progress > 0)
            ? standings[0].UserId
            : null;

        events.Add(new GameFinishedEvent(
            new GameFinishedDto(now, now, winner, standings),
            BuildSaveRequest(standings, winner, now)));
    }

    /// <summary>
    /// Clasificación final: primero quienes terminaron, por orden de llegada; luego el resto por avance,
    /// precisión y orden de entrada a la sala. El ganador es el primero de la lista.
    /// </summary>
    private List<StandingDto> BuildStandings(DateTime endTime)
    {
        var text = _text!.Text;
        var started = _startedAt!.Value;

        return _players.Values
            .OrderBy(p => p.Finished ? 0 : 1)
            .ThenBy(p => p.FinishedAt ?? DateTime.MaxValue)
            .ThenByDescending(p => p.CorrectChars)
            .ThenByDescending(p => TypingScorer.Accuracy(p.Keystrokes, p.Errors))
            .ThenBy(p => p.JoinOrder)
            .Select((p, index) =>
            {
                var wpm = TypingScorer.Wpm(p.CorrectChars, (p.FinishedAt ?? endTime) - started);
                var accuracy = TypingScorer.Accuracy(p.Keystrokes, p.Errors);
                return new StandingDto(
                    index + 1,
                    p.UserId,
                    p.DisplayName,
                    wpm,
                    accuracy,
                    TypingScorer.Progress(p.CorrectChars, text.Length),
                    p.Finished,
                    TypingScorer.Score(wpm, accuracy));
            })
            .ToList();
    }

    /// <summary>Arma la solicitud para la API de resultados (docs/api-resultados.md). No hay nada que registrar si nadie tecleó.</summary>
    private SaveResultRequest? BuildSaveRequest(List<StandingDto> standings, string? winner, DateTime finishedAt)
    {
        if (_players.Values.All(p => p.Keystrokes == 0))
        {
            return null;
        }

        var started = _startedAt!.Value;
        var metadata = JsonSerializer.SerializeToElement(new
        {
            textId = _text!.Id,
            textLength = _text.Text.Length,
            timeLimitSeconds = TimeLimitSeconds,
            durationSeconds = Math.Round((finishedAt - started).TotalSeconds, 1),
            players = standings.Select(s => new
            {
                userId = s.UserId,
                wpm = s.Wpm,
                accuracy = s.Accuracy,
                progress = s.Progress,
                finished = s.Finished,
                finishSeconds = _players[s.UserId].FinishedAt is { } at
                    ? (double?)Math.Round((at - started).TotalSeconds, 1)
                    : null,
            }),
        }, MetadataJson);

        return new SaveResultRequest(
            MatchId,
            ResultValidator.GameType,
            standings.Select(s => new PlayerResultRequest(s.UserId, s.DisplayName, s.Score)).ToList(),
            started,
            finishedAt,
            winner,
            metadata);
    }

    private RoomSnapshotDto BuildSnapshot(string? forUserId, DateTime now)
    {
        var started = State is GameState.Running or GameState.Finished;
        var myTyped = started && forUserId is not null && _players.TryGetValue(forUserId, out var me) ? me.Typed : null;

        return new RoomSnapshotDto(
            MatchId,
            State,
            MinPlayers,
            MaxPlayers,
            TimeLimitSeconds,
            CountdownSeconds,
            PlayerStates(now),
            now,
            _startsAt,
            _startedAt,
            _endsAt,
            started ? _text?.Id : null,
            started ? _text?.Text : null,
            myTyped);
    }

    private List<PlayerStateDto> PlayerStates(DateTime now)
    {
        var textLength = _text?.Text.Length ?? 0;
        var started = _startedAt is not null && State is GameState.Running or GameState.Finished;

        return _players.Values
            .OrderBy(p => p.JoinOrder)
            .Select(p =>
            {
                var wpm = 0.0;
                if (started)
                {
                    var until = p.FinishedAt ?? (State == GameState.Finished ? _finishedAt!.Value : now);
                    wpm = TypingScorer.Wpm(p.CorrectChars, until - _startedAt!.Value);
                }

                return new PlayerStateDto(
                    p.UserId,
                    p.DisplayName,
                    p.Ready,
                    p.Connected,
                    TypingScorer.Progress(p.CorrectChars, textLength),
                    wpm,
                    TypingScorer.Accuracy(p.Keystrokes, p.Errors),
                    p.Finished,
                    p.FinishRank);
            })
            .ToList();
    }
}
