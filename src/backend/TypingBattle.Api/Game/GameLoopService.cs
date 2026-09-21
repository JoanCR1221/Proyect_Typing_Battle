using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Game;

/// <summary>
/// Reloj del juego: cada <see cref="TypingGameOptions.TickMilliseconds"/> hace avanzar todas las salas
/// (cuenta regresiva, límite de tiempo, difusión del progreso) y descarta las que ya no hacen falta.
/// </summary>
public sealed class GameLoopService(
    GameRoomManager rooms,
    RoomEventPublisher publisher,
    IOptions<TypingGameOptions> options,
    TimeProvider time,
    ILogger<GameLoopService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(10, options.Value.TickMilliseconds));
        using var timer = new PeriodicTimer(interval, time);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAllAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal del servicio.
        }
    }

    private async Task TickAllAsync(CancellationToken cancellationToken)
    {
        foreach (var room in rooms.Rooms)
        {
            try
            {
                var events = room.Tick();
                if (events.Count > 0)
                {
                    await publisher.PublishAsync(room, events, cancellationToken);
                }

                if (room.IsExpired())
                {
                    rooms.Remove(room.MatchId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un fallo en una sala no debe detener el reloj de las demás.
                logger.LogError(ex, "Error al avanzar la sala {MatchId}.", room.MatchId);
            }
        }
    }
}
