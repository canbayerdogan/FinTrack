using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure;

/// <summary>
/// CreditCards modülünün kendi veritabanı bağlamı. Şema: <c>cards</c>.
/// Kullanıcı izolasyonu global query filter ile zorunludur; başka kullanıcının
/// kaydı hiçbir sorguda görünmez ve 404'e dönüşür.
/// </summary>
public sealed class CreditCardsDbContext : DbContext
{
    public const string Schema = "cards";

    private readonly ICurrentUser _currentUser;

    public CreditCardsDbContext(DbContextOptions<CreditCardsDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<CreditCard> Cards => Set<CreditCard>();

    private Guid CurrentUserId => _currentUser.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreditCardsDbContext).Assembly);

        modelBuilder.Entity<CreditCard>().HasQueryFilter(card => card.UserId == CurrentUserId);

        // SQLite rowversion üretmez; yalnızca testlerde kullanılan bu sağlayıcıda
        // sürüm alanı uygulama tarafından (test interceptor'ı) atanır.
        if (isSqlite)
        {
            modelBuilder.Entity<CreditCard>().Property(card => card.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
