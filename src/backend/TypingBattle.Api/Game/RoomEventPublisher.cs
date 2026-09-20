using Microsoft.AspNetCore.SignalR;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Matchmaking;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Game;

/// <summary>
/// Convierte los <see cref="RoomEvent"/> de una sala en mensajes a sus jugadores. Al terminar la partida además
/// registra el resultado en la API de resultados (04-persistencia-y-api-juegos.md: lo hace el backend del hub, no el
/// microfrontend) y le avisa a Matchmaking.
/// </summary>
public sealed class RoomEventPublisher(
    IHubContext<TypingHub, ITypingClient> hub,
    IServiceScopeFactory scopes,
    IMatchmakingNotifier matchmaking,
    ILogger<RoomEventPublisher> logger)
{
    public async Task PublishAsync(GameRoom room, IReadOnlyList<RoomEvent> events, CancellationToken cancellationToken = default)
    {
        var group = hub.Clients.Group(room.MatchId);

        foreach (var roomEvent in events)
        {
            switch (roomEvent)
            {
                case RoomUpdatedEvent updated:
                    await group.RoomUpdated(updated.Snapshot);
                    break;
                case GameStartingEvent starting:
                    await group.GameStarting(starting.Payload);
                    break;
                case GameStartedEvent started:
                    await group.GameStarted(started.Payload);
                    break;
                case ProgressUpdatedEvent progress:
                    await group.ProgressUpdated(progress.Payload);
                    break;
                case PlayerFinishedEvent finished:
                    await group.PlayerFinished(finished.Payload);
                    break;
                case GameFinishedEvent gameFinished:
                    await HandleGameFinishedAsync(room, gameFinished, cancellationToken);
                    break;
            }
        }
    }

    private async Task HandleGameFinishedAsync(GameRoom room, GameFinishedEvent finished, CancellationToken cancellationToken)
    {
        // Primero se guarda, para que al recibir GameFinished ya se pueda consultar el resultado por REST.
        var saved = finished.Result is not null && await SaveResultAsync(finished.Result, cancellationToken);
        await hub.Clients.Group(room.MatchId).GameFinished(finished.Payload with { ResultSaved = saved });

        // Avisar a Matchmaking es en segundo plano: un Matchmaking lento no debe frenar a las demás salas.
        var notification = new MatchFinishedNotification(
            room.MatchId, ResultValidator.GameType, finished.Payload.WinnerUserId, finished.Payload.FinishedAt);
        _ = Task.Run(() => matchmaking.NotifyMatchFinishedAsync(notification), CancellationToken.None);
    }

    private async Task<bool> SaveResultAsync(SaveResultRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var results = scope.ServiceProvider.GetRequiredService<IResultsService>();
            var outcome = await results.SaveAsync(request, cancellationToken);

            switch (outcome)
            {
                case SaveResultOutcome.Created:
                    return true;
                case SaveResultOutcome.AlreadyExists:
                    logger.LogWarning("Ya había un resultado para la partida {MatchId}.", request.MatchId);
                    return true;
                case SaveResultOutcome.Invalid invalid:
                    logger.LogError("El resultado de la partida {MatchId} no pasó la validación: {@Errors}", request.MatchId, invalid.Errors);
                    return false;
                default:
                    return false;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo guardar el resultado de la partida {MatchId}.", request.MatchId);
            return false;
        }
    }
}
