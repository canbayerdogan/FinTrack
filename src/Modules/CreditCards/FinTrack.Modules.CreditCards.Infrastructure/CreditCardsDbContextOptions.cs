using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure;

public static class CreditCardsDbContextOptions
{
    /// <summary>Migrations geçmişi şema başına ayrıdır: <c>cards.__EFMigrationsHistory</c>.</summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", CreditCardsDbContext.Schema));
}
