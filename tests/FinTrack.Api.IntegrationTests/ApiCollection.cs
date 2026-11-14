using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>Test sınıfları arasında tek SQL Server konteyneri paylaşılır.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
