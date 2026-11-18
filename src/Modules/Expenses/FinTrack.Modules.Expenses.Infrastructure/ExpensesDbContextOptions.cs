using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure;

public static class ExpensesDbContextOptions
{
    /// <summary>Migrations geçmişi şema başına ayrıdır: <c>expenses.__EFMigrationsHistory</c>.</summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema));

    /// <summary>
    /// ADR-0010: host isteğindeki tek fiziksel bağlantıyı kullanır; middleware
    /// açtığı transaction'ı DbContext'e bağlar.
    /// </summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, DbConnection connection) =>
        options.UseSqlServer(connection, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema));
}
