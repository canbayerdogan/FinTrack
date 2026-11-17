using System.Net;
using System.Net.Http.Json;
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
    [InlineData("/api/v1/reports/_status", "Reporting")]
    public async Task Modul_status_uc_noktasi_calisir(string url, string expectedModule)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ModuleStatusDto>();
        Assert.NotNull(payload);
        Assert.Equal(expectedModule, payload!.Module);
        Assert.Equal("ready", payload.Status);
    }

    [Theory]
    [InlineData("/api/v1/expenses")]
    [InlineData("/api/v1/categories")]
    [InlineData("/api/v1/cards")]
    [InlineData("/api/v1/loans")]
    [InlineData("/api/v1/personal-debts")]
    public async Task Expenses_uc_noktalari_kimlik_dogrulama_ister(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record ModuleStatusDto(string Module, string Status);
}
