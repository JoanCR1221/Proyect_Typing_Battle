using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TypingBattle.Api.Matchmaking;

namespace TypingBattle.Api.Game;

public static class TypingGameServiceCollectionExtensions
{
    /// <summary>Registra las reglas, las salas, el reloj de fondo, SignalR y el aviso a Matchmaking.</summary>
    public static IServiceCollection AddTypingGame(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TypingGameOptions>(configuration.GetSection(TypingGameOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITextProvider, StaticTextProvider>();
        services.AddSingleton<GameRoomManager>();
        services.AddSingleton<RoomEventPublisher>();
        services.AddHostedService<GameLoopService>();
        services.AddMatchmakingNotifier(configuration);

        // Los estados (Waiting, Running...) viajan como texto en camelCase: "running".
        services.AddSignalR().AddJsonProtocol(options =>
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        return services;
    }
}
