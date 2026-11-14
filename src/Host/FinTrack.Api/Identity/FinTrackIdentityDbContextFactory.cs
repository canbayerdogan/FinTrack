using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinTrack.Api.Identity;

/// <summary>
/// <c>dotnet ef</c> komutları için tasarım zamanı bağlantısı.
/// Bağlantı dizesi <c>ConnectionStrings__FinTrackDb</c> ortam değişkeninden okunur.
/// </summary>
public sealed class FinTrackIdentityDbContextFactory : IDesignTimeDbContextFactory<FinTrackIdentityDbContext>
{
    private const string DevelopmentConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=FinTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public FinTrackIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FinTrackDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DevelopmentConnectionString;
        }

        var options = new DbContextOptionsBuilder<FinTrackIdentityDbContext>();
        IdentityDbContextOptions.UseFinTrackSqlServer(options, connectionString);

        return new FinTrackIdentityDbContext(options.Options);
    }
}
