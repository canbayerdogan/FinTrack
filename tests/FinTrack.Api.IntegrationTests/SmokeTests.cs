using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// İskelet ayakta mı: host ayağa kalkıyor, health çalışıyor, her modül kendi
/// uç nokta grubunu yönlendirmeye bağlamış mı.
/// </summary>
public sealed class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_uc_noktasi_200_doner()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/expenses")]
    [InlineData("/api/v1/categories")]
    [InlineData("/api/v1/cards")]
    [InlineData("/api/v1/loans")]
    [InlineData("/api/v1/personal-debts")]
    [InlineData("/api/v1/reports/monthly?year=2026&month=11")]
    [InlineData("/api/v1/reports/debt-summary")]
    [InlineData("/api/v1/reports/upcoming-payments")]
    public async Task Modul_uc_noktalari_kimlik_dogrulama_ister(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
