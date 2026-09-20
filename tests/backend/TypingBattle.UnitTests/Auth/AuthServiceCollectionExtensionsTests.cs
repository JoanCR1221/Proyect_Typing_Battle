using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TypingBattle.Api.Auth;

namespace TypingBattle.UnitTests.Auth;

[Trait("Category", "Unit")]
public class AuthServiceCollectionExtensionsTests
{
    private static IHostEnvironment Environment(string name) =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = name, DisableDefaults = true }).Environment;

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Development_mode_is_refused_outside_development_and_testing(string environment)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTypingAuth(Config(("Auth:Mode", "Development")), Environment(environment)));

        Assert.Contains("Development", ex.Message);
        Assert.Contains(environment, ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task Development_mode_is_allowed_in_development_and_testing(string environment)
    {
        var services = new ServiceCollection().AddLogging();

        services.AddTypingAuth(Config(("Auth:Mode", "Development")), Environment(environment));

        using var provider = services.BuildServiceProvider();
        var scheme = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();
        Assert.Equal(DevelopmentAuthHandler.SchemeName, scheme?.Name);
    }

    [Theory]
    [InlineData("", "api")]
    [InlineData("tenant.auth0.com", "")]
    [InlineData("   ", "   ")]
    public void Auth0_mode_requires_a_domain_and_an_audience(string domain, string audience)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTypingAuth(
                Config(("Auth:Mode", "Auth0"), ("Auth:Domain", domain), ("Auth:Audience", audience)),
                Environment("Production")));

        Assert.Contains("Auth:Domain", ex.Message);
        Assert.Contains("Auth:Audience", ex.Message);
    }

    [Fact]
    public async Task Auth0_mode_is_the_default_and_validates_jwt_bearer_tokens()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddTypingAuth(
            Config(("Auth:Domain", "https://tenant.us.auth0.com/"), ("Auth:Audience", "typing-battle-api")),
            Environment("Production"));

        using var provider = services.BuildServiceProvider();
        var scheme = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme?.Name);

        var jwt = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal("https://tenant.us.auth0.com/", jwt.Authority); // se tolera "https://" y "/" sobrantes
        Assert.Equal("typing-battle-api", jwt.Audience);
        Assert.False(jwt.MapInboundClaims);
    }

    [Fact]
    public void An_unknown_mode_is_rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTypingAuth(Config(("Auth:Mode", "Basic")), Environment("Production")));

        Assert.Contains("Basic", ex.Message);
    }
}
