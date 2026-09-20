using System.Net;

namespace TypingBattle.IntegrationTests;

/// <summary>El hub con la API en modo Auth0: valida JWT de verdad y NO acepta el usuario de desarrollo.</summary>
[Trait("Category", "Integration")]
public class HubAuthTests(JwtApiFactory factory) : IClassFixture<JwtApiFactory>
{
    [Theory]
    [InlineData(false)] // encabezado Authorization (long polling, como las pruebas)
    [InlineData(true)]  // ?access_token= en la URL (WebSocket del navegador, que no puede mandar encabezados)
    public async Task The_hub_accepts_a_valid_token_and_identifies_the_player_by_its_sub_claim(bool tokenInQuery)
    {
        var token = JwtApiFactory.CreateToken("auth0|64f0c1", name: "Ana desde el token");
        await using var player = new HubPlayer(factory, token: token, tokenInQuery: tokenInQuery);
        await player.StartAsync();

        var snapshot = await player.JoinAsync($"jwt-{Guid.NewGuid():N}", displayName: null);

        var me = Assert.Single(snapshot.Players);
        Assert.Equal("auth0|64f0c1", me.UserId);
        Assert.Equal("Ana desde el token", me.DisplayName); // sin displayName del cliente, sale del claim "name"
    }

    [Fact]
    public async Task The_display_name_sent_by_the_client_wins_over_the_token()
    {
        await using var player = new HubPlayer(factory, token: JwtApiFactory.CreateToken("auth0|1", name: "Del token"));
        await player.StartAsync();

        var snapshot = await player.JoinAsync($"jwt-{Guid.NewGuid():N}", "Del contexto del juego");

        Assert.Equal("Del contexto del juego", Assert.Single(snapshot.Players).DisplayName);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("expired")]
    [InlineData("dev-user")]
    public async Task The_hub_rejects_missing_or_invalid_credentials(string kind)
    {
        await using var player = kind switch
        {
            "none" => new HubPlayer(factory),
            "expired" => new HubPlayer(factory, token: JwtApiFactory.CreateToken("auth0|1", expires: DateTime.UtcNow.AddMinutes(-1))),
            _ => new HubPlayer(factory, devUser: "intruso"), // ?dev_user= no cuenta en modo Auth0
        };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => player.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }
}
