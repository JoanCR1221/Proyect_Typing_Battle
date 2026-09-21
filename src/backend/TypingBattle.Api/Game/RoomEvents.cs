using TypingBattle.Api.Results;

namespace TypingBattle.Api.Game;

/// <summary>
/// Algo que ocurrió en una sala y que hay que difundir a sus jugadores. <see cref="GameRoom"/> no toca SignalR:
/// devuelve estos eventos y quien la usa (el hub o el ciclo de fondo) los publica.
/// </summary>
public abstract record RoomEvent;

public sealed record RoomUpdatedEvent(RoomSnapshotDto Snapshot) : RoomEvent;

public sealed record GameStartingEvent(GameStartingDto Payload) : RoomEvent;

public sealed record GameStartedEvent(GameStartedDto Payload) : RoomEvent;

public sealed record ProgressUpdatedEvent(ProgressDto Payload) : RoomEvent;

public sealed record PlayerFinishedEvent(PlayerFinishedDto Payload) : RoomEvent;

/// <param name="Payload">Lo que ven los jugadores.</param>
/// <param name="Result">Resultado a registrar en la API de resultados; <c>null</c> si nadie llegó a teclear.</param>
public sealed record GameFinishedEvent(GameFinishedDto Payload, SaveResultRequest? Result) : RoomEvent;

/// <summary>Respuesta de una operación sobre la sala: si fue aceptada y qué hay que difundir.</summary>
public sealed record RoomResult(bool Ok, string? Error, IReadOnlyList<RoomEvent> Events)
{
    public static RoomResult Success(IReadOnlyList<RoomEvent>? events = null) => new(true, null, events ?? []);

    public static RoomResult Fail(string error) => new(false, error, []);
}
