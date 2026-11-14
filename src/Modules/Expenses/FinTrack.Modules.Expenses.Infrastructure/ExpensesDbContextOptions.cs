using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.Expenses.Infrastructure;

public static class ExpensesDbContextOptions
{
    /// <summary>Migrations geçmişi şema başına ayrıdır: <c>expenses.__EFMigrationsHistory</c>.</summary>
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema));
}
