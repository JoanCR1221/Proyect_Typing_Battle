using System.Security.Claims;
using TypingBattle.Api.Auth;

namespace TypingBattle.UnitTests.Auth;

[Trait("Category", "Unit")]
public class UserIdentityTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void The_user_id_is_the_sub_claim()
    {
        var user = User(("sub", "auth0|64f0c1"));

        Assert.Equal("auth0|64f0c1", user.GetUserId());
    }

    [Fact]
    public void The_user_id_falls_back_to_the_name_identifier_claim()
    {
        var user = User((ClaimTypes.NameIdentifier, "user-9"));

        Assert.Equal("user-9", user.GetUserId());
    }

    [Fact]
    public void A_user_without_an_id_has_none()
    {
        Assert.Null(User(("name", "Ana")).GetUserId());
        Assert.Null(User(("sub", "   ")).GetUserId());
    }

    [Fact]
    public void The_display_name_prefers_name_then_nickname_then_email_then_the_id()
    {
        Assert.Equal("Ana", User(("sub", "u1"), ("name", "Ana"), ("nickname", "ani")).GetDisplayName());
        Assert.Equal("ani", User(("sub", "u1"), ("nickname", "ani"), ("email", "a@x.com")).GetDisplayName());
        Assert.Equal("a@x.com", User(("sub", "u1"), ("email", "a@x.com")).GetDisplayName());
        Assert.Equal("u1", User(("sub", "u1")).GetDisplayName());
        Assert.Equal("Jugador", User().GetDisplayName());
    }
}
