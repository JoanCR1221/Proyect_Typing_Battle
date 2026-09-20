using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// La API en modo Auth0, como en producción: valida JWT de verdad. Solo se reemplaza de dónde salen las claves de firma
/// (una clave de pruebas en lugar de pedirlas a Auth0 por internet); la validación de emisor, audience y vigencia es la real.
/// </summary>
public sealed class JwtApiFactory : WebApplicationFactory<Program>
{
    public const string Domain = "typing-tests.example.com";
    public const string Audience = "typing-battle-api";
    public static readonly string Issuer = $"https://{Domain}/";

    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("clave-de-pruebas-que-tiene-mas-de-32-bytes"));

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"typing-battle-jwt-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Typing", $"Data Source={_databasePath}");
        builder.UseSetting("Auth:Mode", "Auth0");
        builder.UseSetting("Auth:Domain", Domain);
        builder.UseSetting("Auth:Audience", Audience);
        builder.UseSetting("Typing:CountdownSeconds", "0");
        builder.UseSetting("Typing:TickMilliseconds", "20");

        builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            jwt =>
            {
                jwt.Authority = null;
                jwt.ConfigurationManager = null;
                jwt.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = SigningKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            }));
    }

    /// <summary>Crea un token como el que emitiría Auth0. Los parámetros permiten fabricar tokens inválidos.</summary>
    public static string CreateToken(
        string userId,
        string? name = null,
        string audience = Audience,
        DateTime? expires = null,
        SecurityKey? key = null)
    {
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        var claims = new List<Claim> { new("sub", userId) };
        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expiry.AddMinutes(-10),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(key ?? SigningKey, SecurityAlgorithms.HmacSha256),
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm", $"{_databasePath}-journal" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Archivo temporal: si no se puede borrar ahora, el sistema lo limpiará después.
            }
        }
    }
}
