using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure;

public static class CashInflowsDbContextOptions
{
    /// <summary>Migrations geçmişi şema başına ayrıdır: <c>inflows.__EFMigrationsHistory</c>.</summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", CashInflowsDbContext.Schema));
}
