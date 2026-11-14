using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure;

/// <summary>
/// Host, modül DbContext tipini görmeden migration uygular. Sıra host'un
/// <c>IModuleMigration</c> kayıt sırasıdır.
/// </summary>
public sealed class CreditCardsModuleMigration(CreditCardsDbContext db) : IModuleMigration
{
    public string Module => "CreditCards";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);
}
