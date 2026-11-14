using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure;

/// <summary>
/// Expenses modülünün kendi veritabanı bağlamı. Şema: <c>expenses</c>.
/// Kullanıcı izolasyonu global query filter ile zorunludur; başka kullanıcının
/// kaydı hiçbir sorguda görünmez ve 404'e dönüşür.
/// </summary>
public sealed class ExpensesDbContext : DbContext
{
    public const string Schema = "expenses";

    private readonly ICurrentUser _currentUser;

    public ExpensesDbContext(DbContextOptions<ExpensesDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Refund> Refunds => Set<Refund>();

    private Guid CurrentUserId => _currentUser.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpensesDbContext).Assembly);

        modelBuilder.Entity<Category>().HasQueryFilter(category => category.UserId == CurrentUserId);
        modelBuilder.Entity<Expense>().HasQueryFilter(expense => expense.UserId == CurrentUserId);
        modelBuilder.Entity<Refund>().HasQueryFilter(refund => refund.UserId == CurrentUserId);

        // SQLite rowversion üretmez; yalnızca testlerde kullanılan bu sağlayıcıda
        // sürüm alanı uygulama tarafından (test interceptor'ı) atanır.
        if (isSqlite)
        {
            modelBuilder.Entity<Category>().Property(category => category.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<Expense>().Property(expense => expense.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<Refund>().Property(refund => refund.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
