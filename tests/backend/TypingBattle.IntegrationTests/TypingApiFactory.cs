using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace TypingBattle.IntegrationTests;

/// <summary>
/// Levanta la API completa en memoria contra una base SQLite real en un archivo temporal
/// (ver el ADR de motor de BD: no hace falta Docker). Cada fábrica usa su propio archivo.
/// Usa la autenticación de desarrollo: los clientes que crea envían <see cref="DefaultUser"/>.
/// </summary>
public sealed class TypingApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultUser = "test-user";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"typing-battle-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (y no ConfigureAppConfiguration) porque Program.cs lee la configuración
        // mientras registra los servicios, antes de que el host termine de construirse.
        builder.UseSetting("ConnectionStrings:Typing", $"Data Source={_databasePath}");
        builder.UseSetting("Auth:Mode", "Development");

        // Partidas rápidas para las pruebas del hub. El límite de velocidad se desactiva aquí porque los
        // tests envían el texto completo de golpe; el anti-trampa tiene sus propias pruebas unitarias.
        builder.UseSetting("Typing:CountdownSeconds", "0");
        builder.UseSetting("Typing:TimeLimitSeconds", "30");
        builder.UseSetting("Typing:TickMilliseconds", "20");
        builder.UseSetting("Typing:MaxCharsPerSecond", "1000000");
        builder.UseSetting("Typing:MaxBurstChars", "1000000");
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Dev-User", DefaultUser);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // Microsoft.Data.Sqlite mantiene conexiones en un pool; sin liberarlas Windows no deja borrar el archivo.
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm", $"{_databasePath}-journal" })
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Es un archivo temporal: si no se puede borrar ahora, el sistema lo limpiará después.
            }
        }
    }
}
