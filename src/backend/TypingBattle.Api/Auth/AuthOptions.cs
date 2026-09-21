namespace TypingBattle.Api.Auth;

/// <summary>Configuración de autenticación. Sección de configuración: <c>Auth</c>.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string Auth0Mode = "Auth0";
    public const string DevelopmentMode = "Development";

    /// <summary>
    /// <c>Auth0</c> valida los JWT del tenant compartido del proyecto. <c>Development</c> acepta un usuario indicado
    /// por el cliente (sin contraseña) y solo se permite en los entornos Development y Testing.
    /// </summary>
    public string Mode { get; set; } = Auth0Mode;

    /// <summary>Dominio del tenant de Auth0, por ejemplo <c>mi-tenant.us.auth0.com</c>.</summary>
    public string Domain { get; set; } = "";

    /// <summary>Identificador (audience) de la API de Typing Battle registrada en Auth0.</summary>
    public string Audience { get; set; } = "";
}
