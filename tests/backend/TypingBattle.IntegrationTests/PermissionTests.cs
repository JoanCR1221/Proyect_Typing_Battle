using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Permiso <c>games.typing.play</c> (Auth:RequiredPermission), con JWT firmados de verdad: quien no lo tiene recibe
/// 403 (identificado, pero sin permiso), y quien no se identificó sigue recibiendo 401.
/// </summary>
[Trait("Category", "Integration")]
public class PermissionTests(JwtApiFactory jwt) : IClassFixture<JwtApiFactory>
{
    private const string Play = "games.typing.play";
    private const string Url = "/api/games/typing/results/no-existe";

    private WebApplicationFactory<Program> WithPermissionRequired() =>
        jwt.WithWebHostBuilder(builder => builder.UseSetting("Auth:RequiredPermission", Play));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    [Fact]
    public async Task A_token_with_the_permission_is_accepted()
    {
        using var app = WithPermissionRequired();
        var token = JwtApiFactory.CreateToken("auth0|ana", permissions: [Play, "games.trivia.play"]);

        var response = await Client(app, token).GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // pasó la autorización; no hay ese resultado
    }

    [Fact]
    public async Task The_permission_can_also_come_in_the_scope_claim()
    {
        using var app = WithPermissionRequired();
        var token = JwtApiFactory.CreateToken("auth0|ana", scope: $"openid profile {Play}");

        var response = await Client(app, token).GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("sin-permisos")]
    [InlineData("otro-permiso")]
    public async Task A_valid_token_without_the_permission_gets_403(string kind)
    {
        using var app = WithPermissionRequired();
        var token = kind == "sin-permisos"
            ? JwtApiFactory.CreateToken("auth0|ana")
            : JwtApiFactory.CreateToken("auth0|ana", permissions: ["games.trivia.play"]);

        var response = await Client(app, token).GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_token_it_is_still_401_not_403()
    {
        using var app = WithPermissionRequired();

        var response = await Client(app, null).GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_no_permission_is_configured_a_token_without_permissions_still_works()
    {
        // Comportamiento por defecto: la exigencia está apagada.
        var response = await Client(jwt, JwtApiFactory.CreateToken("auth0|ana")).GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_hub_lets_in_a_player_with_the_permission_and_refuses_one_without_it()
    {
        using var app = WithPermissionRequired();
        await using var allowed = new HubPlayer(app, token: JwtApiFactory.CreateToken("auth0|ana", permissions: [Play]));
        await using var denied = new HubPlayer(app, token: JwtApiFactory.CreateToken("auth0|luis"));

        await allowed.StartAsync();
        var snapshot = await allowed.JoinAsync($"perm-{Guid.NewGuid():N}", "Ana");
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => denied.StartAsync());

        Assert.Equal("auth0|ana", Assert.Single(snapshot.Players).UserId);
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }
}

/// <summary>Lo mismo con el usuario de desarrollo, que puede simular permisos con X-Dev-Permissions / dev_permissions.</summary>
[Trait("Category", "Integration")]
public class DevelopmentPermissionTests(TypingApiFactory dev) : IClassFixture<TypingApiFactory>
{
    private const string Play = "games.typing.play";
    private const string Url = "/api/games/typing/results/no-existe";

    private WebApplicationFactory<Program> WithPermissionRequired() =>
        dev.WithWebHostBuilder(builder => builder.UseSetting("Auth:RequiredPermission", Play));

    [Fact]
    public async Task The_development_user_needs_the_permission_too()
    {
        using var app = WithPermissionRequired();

        var without = await app.CreateClient().GetAsync(Url); // el cliente ya envía X-Dev-User
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Permissions", $"otro.permiso, {Play}");
        var with = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, without.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, with.StatusCode);
    }

    [Fact]
    public async Task The_development_hub_accepts_permissions_in_the_url()
    {
        using var app = WithPermissionRequired();
        await using var allowed = new HubPlayer(app, devUser: "ana", devPermissions: Play);
        await using var denied = new HubPlayer(app, devUser: "luis");

        await allowed.StartAsync();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => denied.StartAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }
}
