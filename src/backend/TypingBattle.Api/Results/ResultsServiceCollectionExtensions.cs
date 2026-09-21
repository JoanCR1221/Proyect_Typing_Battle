using Microsoft.EntityFrameworkCore;
using TypingBattle.Api.Results.Persistence;

namespace TypingBattle.Api.Results;

public static class ResultsServiceCollectionExtensions
{
    public const string ConnectionStringName = "Typing";

    /// <summary>Registra la base de datos SQLite (ADR de motor de BD) y el servicio de resultados.</summary>
    public static IServiceCollection AddTypingResults(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? "Data Source=typing-battle.db";

        services.AddDbContext<TypingDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IResultsService, ResultsService>();
        return services;
    }

    /// <summary>
    /// Crea la base de datos y sus tablas si no existen. Es suficiente mientras el esquema no cambie;
    /// cuando cambie hay que pasar a migraciones de EF Core (ver el ADR de motor de BD).
    /// </summary>
    public static async Task InitializeTypingDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TypingDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
