namespace TypingBattle.Api.Results.Persistence;

/// <summary>Una partida jugada. La clave es <see cref="MatchId"/>: solo hay un resultado por partida.</summary>
public sealed class GameResultEntity
{
    public string MatchId { get; set; } = "";
    public string GameType { get; set; } = ResultValidator.GameType;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public string? WinnerUserId { get; set; }

    /// <summary>JSON libre del juego (contrato: <c>metadata</c>). Nunca es nulo; como mínimo es <c>{}</c>.</summary>
    public string MetadataJson { get; set; } = "{}";

    public List<PlayerResultEntity> Players { get; set; } = [];
}

/// <summary>Participación de un jugador en una partida.</summary>
public sealed class PlayerResultEntity
{
    public int Id { get; set; }
    public string MatchId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }

    /// <summary>Puesto final (1 = ganador). Coincide con el orden de <c>players</c> en la solicitud.</summary>
    public int Position { get; set; }

    // Copiados desde metadata.players[] para poder agregar estadísticas en SQL.
    public double? Wpm { get; set; }
    public double? Accuracy { get; set; }
}
