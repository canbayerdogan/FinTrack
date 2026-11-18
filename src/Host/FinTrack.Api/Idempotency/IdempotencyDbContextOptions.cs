using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Idempotency;

public static class IdempotencyDbContextOptions
{
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", IdempotencyDbContext.Schema));

    /// <summary>
    /// ADR-0010: istek kapsamındaki tek fiziksel bağlantıyı kullanır; middleware
    /// açtığı transaction'ı DbContext'e bağlar.
    /// </summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, DbConnection connection) =>
        options.UseSqlServer(connection, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", IdempotencyDbContext.Schema));
}
