using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TypingBattle.Api.Auth;
using TypingBattle.Api.Game;
using TypingBattle.Api.Results;

namespace TypingBattle.Api.Hubs;

/// <summary>
/// Hub propio de Typing Battle (contrato: <c>/hubs/typing</c>, independiente del Lobby Hub de Matchmaking).
/// Flujo del cliente: <c>JoinGame</c> → <c>SetReady</c> → (llega <c>GameStarted</c> con el texto) →
/// <c>SubmitProgress</c> en cada cambio → llega <c>GameFinished</c>. Ver docs/hub-typing.md.
/// </summary>
[Authorize(Policy = TypingPolicies.Play)]
public sealed class TypingHub(GameRoomManager rooms, RoomEventPublisher publisher) : Hub<ITypingClient>
{
    public const string Path = "/hubs/typing";

    private const string MatchKey = "matchId";
    private const string UserKey = "userId";
    private const int MaxTypedLength = 2000;
    private const int MaxNameLength = 100;

    /// <summary>Entra a la sala de la partida (la crea si es el primero) y devuelve su estado actual.</summary>
    /// <param name="matchId">Id de la partida que entregó Matchmaking.</param>
    /// <param name="displayName">Nombre a mostrar (<c>currentUser.displayName</c> del contexto del juego).</param>
    public async Task<RoomSnapshotDto> JoinGame(string matchId, string? displayName = null)
    {
        matchId = matchId?.Trim() ?? "";
        if (matchId.Length is 0 or > ResultValidator.MaxIdLength)
        {
            throw new HubException($"matchId es obligatorio y no puede superar {ResultValidator.MaxIdLength} caracteres.");
        }

        var user = Context.User!;
        var userId = user.GetUserId() ?? throw new HubException("No se pudo identificar al usuario.");

        if (Context.Items.TryGetValue(MatchKey, out var current) && current is string currentMatch && currentMatch != matchId)
        {
            await LeaveCurrentAsync();
        }

        var room = rooms.GetOrCreate(matchId);
        var result = room.Join(userId, CleanName(displayName) ?? user.GetDisplayName(), Context.ConnectionId);
        if (!result.Ok)
        {
            throw new HubException(result.Error);
        }

        Context.Items[MatchKey] = matchId;
        Context.Items[UserKey] = userId;
        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        await publisher.PublishAsync(room, result.Events);
        return room.Snapshot(userId);
    }

    /// <summary>Avisa que este jugador está listo. Cuando todos lo están empieza la cuenta regresiva.</summary>
    public async Task SetReady()
    {
        var (room, userId) = RequireRoom();
        var result = room.SetReady(userId);
        if (!result.Ok)
        {
            throw new HubException(result.Error);
        }

        await publisher.PublishAsync(room, result.Events);
    }

    /// <summary>
    /// Envía lo que el jugador tiene escrito hasta ahora (el texto completo, no solo la última tecla). El servidor
    /// calcula avance, velocidad y precisión; el cliente nunca los informa. Se ignora fuera de la carrera.
    /// </summary>
    public async Task SubmitProgress(string typed)
    {
        if (!TryGetRoom(out var room, out var userId) || typed is { Length: > MaxTypedLength })
        {
            return;
        }

        var result = room.SubmitProgress(userId, typed);
        if (result.Events.Count > 0)
        {
            await publisher.PublishAsync(room, result.Events);
        }
    }

    /// <summary>Sale de la sala. En la sala de espera libera el lugar; en carrera queda como desconectado.</summary>
    public Task LeaveGame() => LeaveCurrentAsync();

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetRoom(out var room, out var userId))
        {
            var result = room.Disconnect(userId, Context.ConnectionId);
            if (result.Events.Count > 0)
            {
                await publisher.PublishAsync(room, result.Events);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveCurrentAsync()
    {
        if (!TryGetRoom(out var room, out var userId))
        {
            return;
        }

        var matchId = room.MatchId;
        Context.Items.Remove(MatchKey);
        Context.Items.Remove(UserKey);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, matchId);
        var result = room.Leave(userId);
        if (result.Events.Count > 0)
        {
            await publisher.PublishAsync(room, result.Events);
        }
    }

    private (GameRoom Room, string UserId) RequireRoom() =>
        TryGetRoom(out var room, out var userId)
            ? (room, userId)
            : throw new HubException("Primero entra a la partida con JoinGame.");

    private bool TryGetRoom(out GameRoom room, out string userId)
    {
        room = null!;
        userId = "";

        if (Context.Items.TryGetValue(MatchKey, out var match) && match is string matchId
            && Context.Items.TryGetValue(UserKey, out var user) && user is string id
            && rooms.TryGet(matchId, out var found))
        {
            room = found;
            userId = id;
            return true;
        }

        return false;
    }

    private static string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }
}
