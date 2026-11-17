using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CashInflows.Infrastructure;

public static class CashInflowsDbContextOptions
{
    /// <summary>Migrations geçmişi şema başına ayrıdır: <c>inflows.__EFMigrationsHistory</c>.</summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", CashInflowsDbContext.Schema));

    /// <summary>
    /// ADR-0009: modüller arası transaction'a katılan yazma işlemleri, host'un
    /// istek kapsamındaki bağlantısını kullanır.
    /// </summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, DbConnection connection) =>
        options.UseSqlServer(connection, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", CashInflowsDbContext.Schema));
}
