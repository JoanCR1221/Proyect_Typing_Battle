using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TypingBattle.Api.Results.Persistence;

public sealed class TypingDbContext(DbContextOptions<TypingDbContext> options) : DbContext(options)
{
    public DbSet<GameResultEntity> GameResults => Set<GameResultEntity>();
    public DbSet<PlayerResultEntity> PlayerResults => Set<PlayerResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite no guarda la zona horaria: se guarda siempre en UTC y al leer se marca como UTC,
        // para que la API responda con sufijo Z (contrato: todas las fechas en UTC).
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        modelBuilder.Entity<GameResultEntity>(entity =>
        {
            entity.HasKey(e => e.MatchId);
            entity.Property(e => e.MatchId).HasMaxLength(ResultValidator.MaxIdLength);
            entity.Property(e => e.GameType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.StartedAt).HasConversion(utcConverter);
            entity.Property(e => e.FinishedAt).HasConversion(utcConverter);
            entity.Property(e => e.WinnerUserId).HasMaxLength(ResultValidator.MaxIdLength);
            entity.Property(e => e.MetadataJson).IsRequired();
            entity.HasIndex(e => e.FinishedAt);
            entity.HasMany(e => e.Players)
                .WithOne()
                .HasForeignKey(p => p.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerResultEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(ResultValidator.MaxIdLength).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(ResultValidator.MaxIdLength).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.MatchId, e.UserId }).IsUnique();
        });
    }
}
