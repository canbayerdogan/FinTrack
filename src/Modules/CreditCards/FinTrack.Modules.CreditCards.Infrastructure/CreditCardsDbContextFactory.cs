using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinTrack.Modules.CreditCards.Infrastructure;

/// <summary>
/// <c>dotnet ef</c> tasarım zamanı bağlamı. Host'a bağımlı değildir; bağlantı
/// dizesi ortam değişkeninden veya geliştirme LocalDB varsayılanından gelir.
/// </summary>
public sealed class CreditCardsDbContextFactory : IDesignTimeDbContextFactory<CreditCardsDbContext>
{
    public CreditCardsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FinTrackDb")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FinTrackDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<CreditCardsDbContext>();
        CreditCardsDbContextOptions.UseFinTrackSqlServer(options, connectionString);

        return new CreditCardsDbContext(options.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;

        public bool IsAuthenticated => false;

        public string PreferredTimeZone => UserTime.DefaultTimeZoneId;

        public Guid RequireUserId() =>
            throw new InvalidOperationException("Tasarım zamanında kullanıcı yoktur.");
    }
}
