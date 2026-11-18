using FinTrack.ModuleContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinTrack.Api.Idempotency;

/// <summary> Modül design-time factory'leriyle aynı desen; connection string host config'inden. </summary>
public sealed class IdempotencyDbContextFactory : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    private const string DevelopmentConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=FinTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FinTrackDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DevelopmentConnectionString;
        }

        var options = new DbContextOptionsBuilder<IdempotencyDbContext>();
        IdempotencyDbContextOptions.UseFinTrackSqlServer(options, connectionString);
        return new IdempotencyDbContext(options.Options);
    }
}

/// <summary> Identity'den sonra, modül migration'larından önce çalışır (docs/11 §5.2). </summary>
public sealed class IdempotencyModuleMigration(IdempotencyDbContext db) : IModuleMigration
{
    public string Module => "Idempotency";

    public Task MigrateAsync(CancellationToken cancellationToken) => db.Database.MigrateAsync(cancellationToken);
}
