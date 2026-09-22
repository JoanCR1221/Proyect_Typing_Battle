using System.Security.Claims;

namespace TypingBattle.Api.Auth;

public static class TypingPolicies
{
    /// <summary>
    /// Política que exigen la API de resultados y el hub. Siempre pide un usuario autenticado; si además se configura
    /// <see cref="AuthOptions.RequiredPermission"/> (por ejemplo <c>games.typing.play</c>), pide ese permiso.
    /// </summary>
    public const string Play = "TypingPlay";
}

/// <summary>Lee los permisos que el token trae para el usuario.</summary>
public static class PermissionCheck
{
    /// <summary>
    /// Indica si el usuario tiene el permiso. Se acepta el claim <c>permissions</c> (Auth0 con RBAC activado y
    /// «Add Permissions in the Access Token») y el claim <c>scope</c> (lista separada por espacios). La comparación
    /// es exacta y distingue mayúsculas: <c>games.typing.play.admin</c> no cuenta como <c>games.typing.play</c>.
    /// </summary>
    public static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        foreach (var claim in user.Claims)
        {
            if (claim.Type == "permissions" && string.Equals(claim.Value, permission, StringComparison.Ordinal))
            {
                return true;
            }

            if (claim.Type == "scope"
                && claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(permission, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
