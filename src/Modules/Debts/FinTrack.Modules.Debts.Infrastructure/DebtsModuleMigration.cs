using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Debts.Infrastructure;

/// <summary>
/// Host, modül DbContext tipini görmeden migration uygular. Sıra host'un
/// <c>IModuleMigration</c> kayıt sırasıdır.
/// </summary>
public sealed class DebtsModuleMigration(DebtsDbContext db) : IModuleMigration
{
    public string Module => "Debts";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);
}
