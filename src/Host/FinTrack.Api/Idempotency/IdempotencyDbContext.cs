using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Idempotency;

/// <summary>
/// Host sahipliğinde <c>shared.IdempotencyKeys</c> tablosuna erişen DbContext
/// (docs/11 §5.2). Modüller bu tipe referans vermez.
/// </summary>
public sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
    : DbContext(options)
{
    public const string Schema = "shared";

    public DbSet<IdempotencyKeyRecord> IdempotencyKeys => Set<IdempotencyKeyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.Entity<IdempotencyKeyRecord>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_IdempotencyKeys_Status",
                    "Status IN (1, 2, 3)");
                table.HasCheckConstraint(
                    "CK_IdempotencyKeys_Completed",
                    "Status <> 2 OR (ResponseStatusCode BETWEEN 200 AND 299 AND ResponseBody IS NOT NULL AND ResponseContentType IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_IdempotencyKeys_Tombstone",
                    "Status <> 3 OR (ResponseBody IS NULL AND ResponseHeaders IS NULL)");
                table.HasCheckConstraint(
                    "CK_IdempotencyKeys_2xx",
                    "ResponseStatusCode IS NULL OR ResponseStatusCode BETWEEN 200 AND 299");
            });
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedOnAdd();

            entity.Property(record => record.ScopeHash)
                .HasColumnType("char(64)").HasMaxLength(64).IsRequired();
            entity.Property(record => record.UserHash)
                .HasColumnType("char(64)").HasMaxLength(64).IsRequired();
            entity.Property(record => record.Key)
                .HasColumnType("nvarchar(36)").HasMaxLength(36).IsRequired();
            entity.Property(record => record.RequestHash)
                .HasColumnType("char(64)").HasMaxLength(64).IsRequired();
            entity.Property(record => record.Status).HasColumnType("tinyint").IsRequired();
            entity.Property(record => record.RequestContentType)
                .HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(record => record.ResponseStatusCode);
            entity.Property(record => record.ResponseContentType)
                .HasColumnType("nvarchar(100)").HasMaxLength(100);
            entity.Property(record => record.ResponseHeaders)
                .HasColumnType("nvarchar(2000)").HasMaxLength(2000);
            entity.Property(record => record.ResponseBody).HasColumnType("nvarchar(max)");
            entity.Property(record => record.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
            entity.Property(record => record.CompletedAtUtc).HasColumnType("datetime2");
            entity.Property(record => record.ExpiresAtUtc).HasColumnType("datetime2").IsRequired();
            entity.Property(record => record.TombstoneExpiresAtUtc).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(record => new { record.ScopeHash, record.Key })
                .IsUnique()
                .HasDatabaseName("UX_IdempotencyKeys_ScopeHash_Key");
            entity.HasIndex(record => new { record.UserHash, record.Key })
                .HasDatabaseName("IX_IdempotencyKeys_UserHash_Key");
            entity.HasIndex(record => record.ExpiresAtUtc)
                .HasDatabaseName("IX_IdempotencyKeys_ExpiresAtUtc");
            entity.HasIndex(record => record.TombstoneExpiresAtUtc)
                .HasDatabaseName("IX_IdempotencyKeys_TombstoneExpiresAtUtc");

        });
    }
}
