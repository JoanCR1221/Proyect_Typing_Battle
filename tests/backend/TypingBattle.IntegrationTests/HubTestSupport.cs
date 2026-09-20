using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TypingBattle.Api.Game;

namespace TypingBattle.IntegrationTests;

/// <summary>Cola de eventos recibidos del hub, con espera y tiempo límite para que las pruebas no se cuelguen.</summary>
internal sealed class Inbox<T>
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    public void Add(T item) => _channel.Writer.TryWrite(item);

    /// <summary>Espera el próximo evento que cumpla la condición; los anteriores que no la cumplen se descartan.</summary>
    public async Task<T> WaitAsync(Func<T, bool>? match = null, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? DefaultTimeout);
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cts.Token))
            {
                while (_channel.Reader.TryRead(out var item))
                {
                    if (match is null || match(item))
                    {
                        return item;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Se agotó la espera de un evento {typeof(T).Name} del hub.");
        }

        throw new InvalidOperationException("El canal del hub se cerró.");
    }
}

/// <summary>Un jugador conectado al hub /hubs/typing del servidor de pruebas (por long polling, sin red real).</summary>
internal sealed class HubPlayer : IAsyncDisposable
{
    /// <param name="devUser">Usuario de desarrollo (parámetro dev_user de la URL). Solo funciona con Auth:Mode=Development.</param>
    /// <param name="token">JWT de acceso. Va en el encabezado Authorization o, con <paramref name="tokenInQuery"/>, en la URL.</param>
    public HubPlayer(WebApplicationFactory<Program> factory, string? devUser = null, string? token = null, bool tokenInQuery = false)
    {
        var query = new List<string>();
        if (devUser is not null)
        {
            query.Add($"dev_user={Uri.EscapeDataString(devUser)}&dev_name={Uri.EscapeDataString(devUser)}");
        }

        if (token is not null && tokenInQuery)
        {
            query.Add($"access_token={Uri.EscapeDataString(token)}");
        }

        var server = factory.Server;
        var url = new Uri(server.BaseAddress, "/hubs/typing" + (query.Count > 0 ? "?" + string.Join("&", query) : ""));

        Connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null && !tokenInQuery)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .AddJsonProtocol(json =>
                json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)))
            .Build();

        Connection.On<RoomSnapshotDto>("RoomUpdated", Rooms.Add);
        Connection.On<GameStartingDto>("GameStarting", Starting.Add);
        Connection.On<GameStartedDto>("GameStarted", Started.Add);
        Connection.On<ProgressDto>("ProgressUpdated", Progress.Add);
        Connection.On<PlayerFinishedDto>("PlayerFinished", PlayerFinished.Add);
        Connection.On<GameFinishedDto>("GameFinished", Finished.Add);
    }

    public HubConnection Connection { get; }
    public Inbox<RoomSnapshotDto> Rooms { get; } = new();
    public Inbox<GameStartingDto> Starting { get; } = new();
    public Inbox<GameStartedDto> Started { get; } = new();
    public Inbox<ProgressDto> Progress { get; } = new();
    public Inbox<PlayerFinishedDto> PlayerFinished { get; } = new();
    public Inbox<GameFinishedDto> Finished { get; } = new();

    public Task StartAsync() => Connection.StartAsync();

    // Los dos argumentos de JoinGame son obligatorios para SignalR aunque displayName pueda ser null.
    public Task<RoomSnapshotDto> JoinAsync(string matchId, string? displayName) =>
        Connection.InvokeAsync<RoomSnapshotDto>("JoinGame", matchId, displayName);

    public Task ReadyAsync() => Connection.InvokeAsync("SetReady");

    public Task TypeAsync(string typed) => Connection.InvokeAsync("SubmitProgress", typed);

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
