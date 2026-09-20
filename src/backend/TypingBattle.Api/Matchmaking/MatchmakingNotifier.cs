using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Matchmaking;

/// <summary>Configuración del aviso a Matchmaking. Sección de configuración: <c>Matchmaking</c>.</summary>
public sealed class MatchmakingOptions
{
    public const string SectionName = "Matchmaking";

    /// <summary>URL base del servicio de Matchmaking. Vacía = no se avisa (solo se deja constancia en el log).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Ruta a la que se hace POST al terminar la partida. <b>Es una suposición</b>: el contrato todavía no define cómo
    /// un juego avisa el fin de partida (duda 1 de docs/contratos-pendientes.md), por eso es configurable.
    /// </summary>
    public string FinishPath { get; set; } = "/api/matches/{matchId}/finish";

    public int TimeoutSeconds { get; set; } = 5;
}

public sealed record MatchFinishedNotification(string MatchId, string GameType, string? WinnerUserId, DateTime FinishedAt);

/// <summary>Le avisa a Matchmaking que la partida terminó, para que pueda limpiar la sala. No le entrega el resultado.</summary>
public interface IMatchmakingNotifier
{
    /// <summary>Devuelve <c>true</c> si Matchmaking recibió el aviso. Nunca lanza: un fallo aquí no debe afectar la partida.</summary>
    Task<bool> NotifyMatchFinishedAsync(MatchFinishedNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>Se usa mientras <c>Matchmaking:BaseUrl</c> no esté configurada.</summary>
public sealed class NoOpMatchmakingNotifier(ILogger<NoOpMatchmakingNotifier> logger) : IMatchmakingNotifier
{
    public Task<bool> NotifyMatchFinishedAsync(MatchFinishedNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Partida {MatchId} terminada. Matchmaking:BaseUrl no está configurada: no se avisa a Matchmaking.",
            notification.MatchId);
        return Task.FromResult(false);
    }
}

public sealed class HttpMatchmakingNotifier(
    HttpClient http,
    IOptions<MatchmakingOptions> options,
    ILogger<HttpMatchmakingNotifier> logger) : IMatchmakingNotifier
{
    public async Task<bool> NotifyMatchFinishedAsync(
        MatchFinishedNotification notification, CancellationToken cancellationToken = default)
    {
        var path = options.Value.FinishPath.Replace("{matchId}", Uri.EscapeDataString(notification.MatchId));

        try
        {
            using var response = await http.PostAsJsonAsync(path, notification, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            logger.LogWarning(
                "Matchmaking respondió {StatusCode} al avisar el fin de la partida {MatchId}.",
                (int)response.StatusCode, notification.MatchId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "No se pudo avisar a Matchmaking el fin de la partida {MatchId}.", notification.MatchId);
        }

        return false;
    }
}

public static class MatchmakingServiceCollectionExtensions
{
    public static IServiceCollection AddMatchmakingNotifier(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(MatchmakingOptions.SectionName);
        services.Configure<MatchmakingOptions>(section);

        var options = section.Get<MatchmakingOptions>() ?? new MatchmakingOptions();
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddSingleton<IMatchmakingNotifier, NoOpMatchmakingNotifier>();
        }
        else
        {
            services.AddHttpClient<IMatchmakingNotifier, HttpMatchmakingNotifier>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            });
        }

        return services;
    }
}
