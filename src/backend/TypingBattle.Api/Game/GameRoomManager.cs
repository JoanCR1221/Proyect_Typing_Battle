using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Game;

/// <summary>Las salas activas, una por <c>matchId</c>. Vive en memoria: una partida es corta y su resultado se guarda al terminar.</summary>
public sealed class GameRoomManager(IOptions<TypingGameOptions> options, ITextProvider texts, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new(StringComparer.Ordinal);

    public GameRoom GetOrCreate(string matchId) =>
        _rooms.GetOrAdd(matchId, id => new GameRoom(id, options.Value, texts, time));

    public bool TryGet(string matchId, out GameRoom room)
    {
        var found = _rooms.TryGetValue(matchId, out var existing);
        room = existing!;
        return found;
    }

    public IReadOnlyCollection<GameRoom> Rooms => _rooms.Values.ToList();

    public void Remove(string matchId) => _rooms.TryRemove(matchId, out _);
}
