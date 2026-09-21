using TypingBattle.Api.Game;

namespace TypingBattle.Api.Hubs;

/// <summary>
/// Eventos que el servidor envía a los jugadores de una partida (grupo = <c>matchId</c>).
/// El detalle de cada carga está en docs/hub-typing.md.
/// </summary>
public interface ITypingClient
{
    /// <summary>Cambió la sala de espera: entró o salió alguien, o cambió el estado de listos.</summary>
    Task RoomUpdated(RoomSnapshotDto snapshot);

    /// <summary>Todos están listos: empezó la cuenta regresiva.</summary>
    Task GameStarting(GameStartingDto payload);

    /// <summary>Terminó la cuenta regresiva: aquí llega el texto a escribir.</summary>
    Task GameStarted(GameStartedDto payload);

    /// <summary>Progreso en vivo de todos los jugadores (como máximo unas 4 veces por segundo).</summary>
    Task ProgressUpdated(ProgressDto payload);

    /// <summary>Un jugador terminó de escribir el texto.</summary>
    Task PlayerFinished(PlayerFinishedDto payload);

    /// <summary>Terminó la carrera: clasificación final y ganador.</summary>
    Task GameFinished(GameFinishedDto payload);
}
