using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure;

/// <summary>
/// CashInflows modülünün kendi veritabanı bağlamı. Şema: <c>inflows</c>.
/// Kullanıcı izolasyonu global query filter ile zorunludur; başka kullanıcının
/// kaydı hiçbir sorguda görünmez ve 404'e dönüşür.
/// </summary>
public sealed class CashInflowsDbContext : DbContext
{
    public const string Schema = "inflows";

    private readonly ICurrentUser _currentUser;

    public CashInflowsDbContext(DbContextOptions<CashInflowsDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<CashInflow> CashInflows => Set<CashInflow>();

    private Guid CurrentUserId => _currentUser.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CashInflowsDbContext).Assembly);

        modelBuilder.Entity<CashInflow>().HasQueryFilter(inflow => inflow.UserId == CurrentUserId);

        // SQLite rowversion üretmez; yalnızca testlerde kullanılan bu sağlayıcıda
        // sürüm alanı uygulama tarafından (test interceptor'ı) atanır.
        if (isSqlite)
        {
            modelBuilder.Entity<CashInflow>().Property(inflow => inflow.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
