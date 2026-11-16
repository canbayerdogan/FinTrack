using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure;

/// <summary>
/// Host, modül DbContext tipini görmeden migration uygular. Sıra host'un
/// <c>IModuleMigration</c> kayıt sırasıdır.
/// </summary>
public sealed class CashInflowsModuleMigration(CashInflowsDbContext db) : IModuleMigration
{
    public string Module => "CashInflows";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);
}
