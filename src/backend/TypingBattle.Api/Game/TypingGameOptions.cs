namespace TypingBattle.Api.Game;

/// <summary>Reglas de la partida. Sección de configuración: <c>Typing</c>.</summary>
public sealed class TypingGameOptions
{
    public const string SectionName = "Typing";

    /// <summary>Cuenta regresiva entre que todos están listos y que aparece el texto.</summary>
    public int CountdownSeconds { get; set; } = 3;

    /// <summary>Duración máxima de la carrera.</summary>
    public int TimeLimitSeconds { get; set; } = 60;

    /// <summary>Jugadores conectados y listos que se necesitan para empezar.</summary>
    public int MinPlayers { get; set; } = 2;

    public int MaxPlayers { get; set; } = 10;

    /// <summary>Cada cuánto el servidor avanza los estados de las salas y difunde el progreso.</summary>
    public int TickMilliseconds { get; set; } = 250;

    /// <summary>Cuánto se conserva en memoria una sala terminada o vacía.</summary>
    public int RoomTtlSeconds { get; set; } = 60;

    // Anti-trampa: el servidor ignora un progreso más rápido de lo que una persona puede teclear.

    /// <summary>Velocidad máxima creíble. 35 caracteres por segundo equivalen a unas 420 palabras por minuto.</summary>
    public int MaxCharsPerSecond { get; set; } = 35;

    /// <summary>Holgura para ráfagas de teclas y latencia de red.</summary>
    public int MaxBurstChars { get; set; } = 20;
}
