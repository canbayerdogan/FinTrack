using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure;

/// <summary>
/// Host, modül DbContext tipini görmeden migration uygular. Sıra host'un
/// <c>IModuleMigration</c> kayıt sırasıdır.
/// </summary>
public sealed class ExpensesModuleMigration(ExpensesDbContext db) : IModuleMigration
{
    public string Module => "Expenses";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);
}
