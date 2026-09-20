namespace TypingBattle.Api.Game;

// Cargas que viajan por el hub /hubs/typing (ver docs/hub-typing.md). Todas las fechas van en UTC con sufijo Z,
// y cada mensaje trae ServerTime para que el cliente corrija la diferencia entre su reloj y el del servidor.

public enum GameState
{
    /// <summary>Esperando jugadores y que todos estén listos.</summary>
    Waiting,

    /// <summary>Todos listos: cuenta regresiva antes de mostrar el texto.</summary>
    Countdown,

    /// <summary>Carrera en curso.</summary>
    Running,

    /// <summary>Carrera terminada.</summary>
    Finished,
}

/// <summary>Estado de un jugador para pintar la sala y la clasificación en vivo.</summary>
public sealed record PlayerStateDto(
    string UserId,
    string DisplayName,
    bool Ready,
    bool Connected,
    double Progress,
    double Wpm,
    double Accuracy,
    bool Finished,
    int? Rank);

/// <summary>Foto completa de la sala. La devuelve <c>JoinGame</c> y sirve para reconectarse a mitad de carrera.</summary>
public sealed record RoomSnapshotDto(
    string MatchId,
    GameState State,
    int MinPlayers,
    int MaxPlayers,
    int TimeLimitSeconds,
    int CountdownSeconds,
    IReadOnlyList<PlayerStateDto> Players,
    DateTime ServerTime,
    DateTime? StartsAt,
    DateTime? StartedAt,
    DateTime? EndsAt,
    string? TextId,
    string? Text,
    string? MyTyped);

public sealed record GameStartingDto(DateTime ServerTime, DateTime StartsAt, int CountdownSeconds);

public sealed record GameStartedDto(
    DateTime ServerTime,
    DateTime StartedAt,
    DateTime EndsAt,
    int TimeLimitSeconds,
    string TextId,
    string Text);

public sealed record ProgressDto(DateTime ServerTime, IReadOnlyList<PlayerStateDto> Players);

public sealed record PlayerFinishedDto(string UserId, int Rank, double Wpm, double Accuracy);

/// <summary>Una fila de la clasificación final.</summary>
public sealed record StandingDto(
    int Rank,
    string UserId,
    string DisplayName,
    double Wpm,
    double Accuracy,
    double Progress,
    bool Finished,
    int Score);

public sealed record GameFinishedDto(
    DateTime ServerTime,
    DateTime FinishedAt,
    string? WinnerUserId,
    IReadOnlyList<StandingDto> Standings)
{
    /// <summary>Si el resultado quedó guardado en la base de datos (ya se puede consultar en la API REST).</summary>
    public bool ResultSaved { get; init; }
}
