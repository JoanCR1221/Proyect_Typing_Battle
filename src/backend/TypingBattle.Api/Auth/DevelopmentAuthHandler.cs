using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TypingBattle.Api.Auth;

/// <summary>
/// Autenticación SOLO para desarrollo local y pruebas: confía en el usuario que declare el cliente, sin contraseña ni token.
/// Sirve para trabajar sin cuenta de Auth0 (por ejemplo con el modo standalone del microfrontend). Nunca debe llegar a producción:
/// <see cref="AuthServiceCollectionExtensions.AddTypingAuth"/> se niega a registrarla fuera de Development y Testing.
/// </summary>
/// <remarks>
/// Identidad por encabezado (<c>X-Dev-User</c>, <c>X-Dev-Name</c>) o, para el hub (un WebSocket no puede mandar
/// encabezados desde el navegador), por parámetros de la URL (<c>dev_user</c>, <c>dev_name</c>).
/// </remarks>
public sealed class DevelopmentAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Dev-User"].FirstOrDefault() ?? Request.Query["dev_user"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        userId = userId.Trim();
        var name = Request.Headers["X-Dev-Name"].FirstOrDefault() ?? Request.Query["dev_name"].FirstOrDefault();
        var identity = new ClaimsIdentity(
            [new Claim("sub", userId), new Claim("name", string.IsNullOrWhiteSpace(name) ? userId : name.Trim())],
            SchemeName,
            nameType: "name",
            roleType: "role");

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
