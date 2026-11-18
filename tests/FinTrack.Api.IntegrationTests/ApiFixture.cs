using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
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

    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.Ordinal);

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>Testlerin doğrudan doğrulama/senaryo verisi yazması için konteyner bağlantısı.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "FinTrackTests",
        }.ConnectionString;

        // Program.cs bu değerleri host kurulurken doğrudan okur. WithWebHostBuilder +
        // ConfigureAppConfiguration bu noktada geç kalır; appsettings.Development.json
        // içindeki LocalDB dizesi kazanır. Env var sağlayıcısı appsettings'ten sonra
        // geldiği için geçersiz kılma garanti edilir.
        ApplyEnvironmentOverrides(new Dictionary<string, string?>
        {
            ["ConnectionStrings__FinTrackDb"] = connectionString,
            ["Database__MigrateOnStartup"] = "true",
            ["Jwt__Issuer"] = "FinTrack.Tests",
            ["Jwt__Audience"] = "FinTrack.Tests",
            ["Jwt__SigningKey"] = "test-signing-key-that-is-long-enough-1234567890",
            ["Jwt__AccessTokenMinutes"] = "15",
            ["Jwt__RefreshTokenDays"] = "30",
        });

        ConnectionString = connectionString;
        Factory = CreateFactory();

        using var client = Factory.CreateClient();
        using var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
        RestoreEnvironment();
    }

    /// <summary>
    /// Aynı veritabanını paylaşan ikinci bir uygulama instance'ı (W6 / S4).
    /// </summary>
    public WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    private void ApplyEnvironmentOverrides(IReadOnlyDictionary<string, string?> values)
    {
        foreach (var (key, value) in values)
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void RestoreEnvironment()
    {
        foreach (var (key, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
