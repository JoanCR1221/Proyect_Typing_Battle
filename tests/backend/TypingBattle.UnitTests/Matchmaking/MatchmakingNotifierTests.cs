using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TypingBattle.Api.Matchmaking;

namespace TypingBattle.UnitTests.Matchmaking;

[Trait("Category", "Unit")]
public class MatchmakingNotifierTests
{
    private static readonly MatchFinishedNotification Notification =
        new("partida 7", "typing", "user-001", new DateTime(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc));

    private sealed class FakeHandler(HttpStatusCode status = HttpStatusCode.NoContent, Exception? failure = null) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method, request.RequestUri!.AbsoluteUri, body));

            return failure is null ? new HttpResponseMessage(status) : throw failure;
        }
    }

    private static HttpMatchmakingNotifier Notifier(FakeHandler handler, string path = "/api/matches/{matchId}/finish") =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://matchmaking.test/") },
            Options.Create(new MatchmakingOptions { BaseUrl = "http://matchmaking.test/", FinishPath = path }),
            NullLogger<HttpMatchmakingNotifier>.Instance);

    [Fact]
    public async Task Posts_the_notification_to_the_configured_path_with_the_match_id_escaped()
    {
        var handler = new FakeHandler();

        var delivered = await Notifier(handler).NotifyMatchFinishedAsync(Notification);

        Assert.True(delivered);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("http://matchmaking.test/api/matches/partida%207/finish", call.Url);
        Assert.Contains("\"matchId\":\"partida 7\"", call.Body);
        Assert.Contains("\"winnerUserId\":\"user-001\"", call.Body);
        Assert.Contains("\"finishedAt\":\"2026-09-02T20:00:00Z\"", call.Body);
    }

    [Fact]
    public async Task The_path_is_configurable()
    {
        var handler = new FakeHandler();

        await Notifier(handler, "/internal/games/{matchId}/done").NotifyMatchFinishedAsync(Notification);

        Assert.Equal("http://matchmaking.test/internal/games/partida%207/done", handler.Calls.Single().Url);
    }

    [Fact]
    public async Task It_reports_false_when_matchmaking_answers_with_an_error()
    {
        var delivered = await Notifier(new FakeHandler(HttpStatusCode.InternalServerError)).NotifyMatchFinishedAsync(Notification);

        Assert.False(delivered);
    }

    [Fact]
    public async Task It_reports_false_instead_of_throwing_when_matchmaking_is_unreachable()
    {
        var handler = new FakeHandler(failure: new HttpRequestException("sin conexión"));

        var delivered = await Notifier(handler).NotifyMatchFinishedAsync(Notification);

        Assert.False(delivered);
    }

    [Fact]
    public async Task It_reports_false_on_timeout()
    {
        var handler = new FakeHandler(failure: new TaskCanceledException("timeout"));

        Assert.False(await Notifier(handler).NotifyMatchFinishedAsync(Notification));
    }

    [Fact]
    public async Task The_no_op_notifier_never_delivers_and_never_throws()
    {
        var notifier = new NoOpMatchmakingNotifier(NullLogger<NoOpMatchmakingNotifier>.Instance);

        Assert.False(await notifier.NotifyMatchFinishedAsync(Notification));
    }

    [Theory]
    [InlineData("", typeof(NoOpMatchmakingNotifier))]
    [InlineData("http://matchmaking.test", typeof(HttpMatchmakingNotifier))]
    public void The_http_notifier_is_only_registered_when_the_base_url_is_configured(string baseUrl, Type expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("Matchmaking:BaseUrl", baseUrl)])
            .Build();
        var services = new ServiceCollection().AddLogging();

        services.AddMatchmakingNotifier(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.IsType(expected, provider.GetRequiredService<IMatchmakingNotifier>());
    }
}
