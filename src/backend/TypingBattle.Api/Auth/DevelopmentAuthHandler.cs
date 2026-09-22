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
/// Identidad por encabezado (<c>X-Dev-User</c>, <c>X-Dev-Name</c>, <c>X-Dev-Permissions</c>) o, para el hub (un
/// WebSocket no puede mandar encabezados desde el navegador), por parámetros de la URL (<c>dev_user</c>, <c>dev_name</c>,
/// <c>dev_permissions</c>).
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
        var claims = new List<Claim> { new("sub", userId), new("name", string.IsNullOrWhiteSpace(name) ? userId : name.Trim()) };

        // Permisos simulados para probar Auth:RequiredPermission sin Auth0: «games.typing.play» (varios separados por coma).
        var permissions = Request.Headers["X-Dev-Permissions"].FirstOrDefault() ?? Request.Query["dev_permissions"].FirstOrDefault();
        foreach (var permission in (permissions ?? "").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim("permissions", permission));
        }

        var identity = new ClaimsIdentity(
            claims,
            SchemeName,
            nameType: "name",
            roleType: "role");

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
