using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Identity;

/// <summary>
/// Identity DbContext'in SQL Server ayarları; runtime ve tasarım zamanı aynı geçmiş tablosunu kullanır.
/// </summary>
public static class IdentityDbContextOptions
{
    public static void UseFinTrackSqlServer(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"));
    }
}
