using System.Net;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;

namespace TypingBattle.IntegrationTests;

/// <summary>La autenticación de desarrollo: sirve para trabajar sin Auth0 y solo vive en Development y Testing.</summary>
[Trait("Category", "Integration")]
public class DevelopmentAuthTests(TypingApiFactory factory) : IClassFixture<TypingApiFactory>
{
    [Fact]
    public async Task The_results_api_rejects_requests_without_a_user()
    {
        var anonymous = factory.Server.CreateClient(); // sin el encabezado que la fábrica agrega a sus clientes

        var response = await anonymous.GetAsync("/api/games/typing/results/lo-que-sea");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_results_api_accepts_the_development_user_header()
    {
        var response = await factory.CreateClient().GetAsync("/api/games/typing/results/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // pasó la autenticación y no hay ese resultado
    }

    [Fact]
    public async Task Health_is_public()
    {
        var response = await factory.Server.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>La API en modo Auth0: valida JWT de verdad y NO acepta el usuario de desarrollo.</summary>
[Trait("Category", "Integration")]
public class JwtAuthTests(JwtApiFactory factory) : IClassFixture<JwtApiFactory>
{
    private const string Url = "/api/games/typing/results/no-existe";

    private HttpClient ClientWithToken(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task A_valid_token_is_accepted()
    {
        var response = await ClientWithToken(JwtApiFactory.CreateToken("auth0|ana")).GetAsync(Url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // pasó la autenticación y no hay ese resultado
    }

    [Fact]
    public async Task Requests_without_a_token_are_rejected()
    {
        var response = await factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-key")]
    [InlineData("garbage")]
    public async Task Invalid_tokens_are_rejected(string kind)
    {
        var token = kind switch
        {
            "expired" => JwtApiFactory.CreateToken("auth0|ana", expires: DateTime.UtcNow.AddMinutes(-1)),
            "wrong-audience" => JwtApiFactory.CreateToken("auth0|ana", audience: "otra-api"),
            "wrong-key" => JwtApiFactory.CreateToken("auth0|ana", key: new SymmetricSecurityKey(new byte[32].Select(_ => (byte)7).ToArray())),
            _ => "esto.no-es.un-jwt",
        };

        var response = await ClientWithToken(token).GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_development_user_header_is_ignored_in_auth0_mode()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "intruso");

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
