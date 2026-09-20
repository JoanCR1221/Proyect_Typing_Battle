using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TypingBattle.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>Los hubs viven bajo esta ruta; ahí el token puede venir en la URL (ver <see cref="AddTypingAuth"/>).</summary>
    private const string HubsPathPrefix = "/hubs";

    /// <summary>
    /// Registra la autenticación según <c>Auth:Mode</c>. Falla al arrancar, con un mensaje claro, si la
    /// configuración es incompleta o si se intenta el modo Development fuera de Development y Testing.
    /// </summary>
    public static IServiceCollection AddTypingAuth(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        if (string.Equals(options.Mode, AuthOptions.DevelopmentMode, StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    $"Auth:Mode={AuthOptions.DevelopmentMode} solo se permite en los entornos Development y Testing " +
                    $"(entorno actual: {environment.EnvironmentName}). Use Auth:Mode={AuthOptions.Auth0Mode}.");
            }

            services
                .AddAuthentication(DevelopmentAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthHandler>(DevelopmentAuthHandler.SchemeName, _ => { });
        }
        else if (string.Equals(options.Mode, AuthOptions.Auth0Mode, StringComparison.OrdinalIgnoreCase))
        {
            var domain = NormalizeDomain(options.Domain);
            if (domain.Length == 0 || string.IsNullOrWhiteSpace(options.Audience))
            {
                throw new InvalidOperationException(
                    "Con Auth:Mode=Auth0 hay que configurar Auth:Domain (por ejemplo mi-tenant.us.auth0.com) y " +
                    "Auth:Audience (el identificador de la API en Auth0). Para desarrollo local sin Auth0 use " +
                    "Auth__Mode=Development.");
            }

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = $"https://{domain}/";
                    jwt.Audience = options.Audience;

                    // Sin transformar los nombres de los claims: "sub" sigue siendo "sub".
                    jwt.MapInboundClaims = false;

                    // Un WebSocket del navegador no puede enviar el encabezado Authorization, así que SignalR manda
                    // el token en la URL (?access_token=...). Solo se acepta ahí para los hubs.
                    jwt.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var token = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(HubsPathPrefix))
                            {
                                context.Token = token;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }
        else
        {
            throw new InvalidOperationException(
                $"Auth:Mode '{options.Mode}' no es válido. Use '{AuthOptions.Auth0Mode}' o '{AuthOptions.DevelopmentMode}'.");
        }

        services.AddAuthorization();
        return services;
    }

    private static string NormalizeDomain(string domain)
    {
        var value = domain.Trim();
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = value["https://".Length..];
        }

        return value.TrimEnd('/');
    }
}
