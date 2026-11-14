using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Tüm API entegrasyon testleri için tek bir SQL Server konteyneri ve uygulama
/// host'u. Migration, host ayağa kalkarken <c>Database:MigrateOnStartup</c> ile
/// identity ve modül migration'larını uygular.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "FinTrackTests",
        }.ConnectionString;

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:FinTrackDb"] = connectionString,
                        ["Database:MigrateOnStartup"] = "true",
                        ["Jwt:Issuer"] = "FinTrack.Tests",
                        ["Jwt:Audience"] = "FinTrack.Tests",
                        ["Jwt:SigningKey"] = "test-signing-key-that-is-long-enough-1234567890",
                        ["Jwt:AccessTokenMinutes"] = "15",
                        ["Jwt:RefreshTokenDays"] = "30",
                    });
                });
            });

        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}
