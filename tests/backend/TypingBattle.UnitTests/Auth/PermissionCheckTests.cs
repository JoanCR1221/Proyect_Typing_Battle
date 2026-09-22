using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TypingBattle.Api.Auth;

namespace TypingBattle.UnitTests.Auth;

[Trait("Category", "Unit")]
public class PermissionCheckTests
{
    private const string Play = "games.typing.play";

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)).Append(new Claim("sub", "u1")), "test"));

    [Fact]
    public void The_permissions_claim_grants_the_permission()
    {
        var user = User(("permissions", "games.trivia.play"), ("permissions", Play));

        Assert.True(PermissionCheck.HasPermission(user, Play));
    }

    [Fact]
    public void The_scope_claim_grants_the_permission_when_it_is_one_of_the_listed_words()
    {
        Assert.True(PermissionCheck.HasPermission(User(("scope", $"openid profile {Play}")), Play));
        Assert.False(PermissionCheck.HasPermission(User(("scope", "openid profile")), Play));
    }

    [Theory]
    [InlineData("games.typing.play.admin")]
    [InlineData("games.typing")]
    [InlineData("GAMES.TYPING.PLAY")]
    [InlineData("xgames.typing.play")]
    public void The_match_is_exact_and_case_sensitive(string other)
    {
        Assert.False(PermissionCheck.HasPermission(User(("permissions", other)), Play));
        Assert.False(PermissionCheck.HasPermission(User(("scope", other)), Play));
    }

    [Fact]
    public void A_user_without_permissions_does_not_have_it()
    {
        Assert.False(PermissionCheck.HasPermission(User(), Play));
    }

    private static IAuthorizationService Authorization(string? requiredPermission)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Development",
                ["Auth:RequiredPermission"] = requiredPermission,
            })
            .Build();
        var environment = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development", DisableDefaults = true }).Environment;

        var services = new ServiceCollection().AddLogging();
        services.AddTypingAuth(configuration, environment);
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Without_a_required_permission_any_authenticated_user_passes(string? required)
    {
        var authorization = Authorization(required);

        Assert.True((await authorization.AuthorizeAsync(User(), null, TypingPolicies.Play)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, TypingPolicies.Play)).Succeeded); // anónimo
    }

    [Fact]
    public async Task With_a_required_permission_only_users_who_have_it_pass()
    {
        var authorization = Authorization($"  {Play}  "); // se toleran espacios en la configuración

        Assert.True((await authorization.AuthorizeAsync(User(("permissions", Play)), null, TypingPolicies.Play)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(User(("scope", $"openid {Play}")), null, TypingPolicies.Play)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(User(), null, TypingPolicies.Play)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(User(("permissions", "games.trivia.play")), null, TypingPolicies.Play)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, TypingPolicies.Play)).Succeeded);
    }
}
