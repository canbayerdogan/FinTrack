using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure;

/// <summary>
/// Debts modülünün kendi veritabanı bağlamı. Şema: <c>debts</c>.
/// Kullanıcı izolasyonu global query filter ile zorunludur; başka kullanıcının
/// kaydı hiçbir sorguda görünmez ve 404'e dönüşür.
/// </summary>
public sealed class DebtsDbContext : DbContext
{
    public const string Schema = "debts";

    private readonly ICurrentUser _currentUser;

    public DebtsDbContext(DbContextOptions<DebtsDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<Installment> Installments => Set<Installment>();

    public DbSet<InstallmentPayment> InstallmentPayments => Set<InstallmentPayment>();

    public DbSet<PersonalDebt> PersonalDebts => Set<PersonalDebt>();

    public DbSet<PersonalDebtRepayment> Repayments => Set<PersonalDebtRepayment>();

    private Guid CurrentUserId => _currentUser.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DebtsDbContext).Assembly);

        modelBuilder.Entity<Loan>().HasQueryFilter(loan => loan.UserId == CurrentUserId);
        modelBuilder.Entity<Installment>().HasQueryFilter(installment => installment.UserId == CurrentUserId);
        modelBuilder.Entity<InstallmentPayment>()
            .HasQueryFilter(payment => payment.UserId == CurrentUserId);
        modelBuilder.Entity<PersonalDebt>().HasQueryFilter(debt => debt.UserId == CurrentUserId);
        modelBuilder.Entity<PersonalDebtRepayment>()
            .HasQueryFilter(repayment => repayment.UserId == CurrentUserId);

        // SQLite rowversion üretmez; yalnızca testlerde kullanılan bu sağlayıcıda
        // sürüm alanı uygulama tarafından (test interceptor'ı) atanır.
        if (isSqlite)
        {
            modelBuilder.Entity<Loan>().Property(loan => loan.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<InstallmentPayment>().Property(payment => payment.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<PersonalDebt>().Property(debt => debt.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            modelBuilder.Entity<PersonalDebtRepayment>().Property(repayment => repayment.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
