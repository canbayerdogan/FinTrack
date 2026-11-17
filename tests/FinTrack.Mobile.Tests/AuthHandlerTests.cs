using System.Net;
using System.Net.Http.Json;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Tests.Fakes;
using Xunit;

namespace FinTrack.Mobile.Tests;

public sealed class AuthHandlerTests
{
    private static readonly Uri BaseAddress = new("https://api.test/api/v1/");

    [Fact]
    public async Task Bearer_token_her_isteye_eklenir()
    {
        var store = new FakeTokenStore();
        await store.SaveAsync(new TokenPair("old-access", "old-refresh"));
        var queue = new QueueHandler(QueueHandler.Empty(HttpStatusCode.OK));
        using var client = CreateClient(store, new QueueHandler(), queue);

        await client.GetAsync("reports/debt-summary");

        Assert.Equal("Bearer", queue.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("old-access", queue.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Unauthorized_sonrasi_refresh_yapilir_ve_get_tekrar_edilir()
    {
        var store = new FakeTokenStore();
        await store.SaveAsync(new TokenPair("old-access", "old-refresh"));
        var refreshQueue = RefreshHandler(HttpStatusCode.OK);
        var queue = new QueueHandler(
            QueueHandler.Json(HttpStatusCode.Unauthorized, Problem("Unauthorized", "unauthorized")),
            QueueHandler.Empty(HttpStatusCode.OK));
        using var client = CreateClient(store, refreshQueue, queue);

        var response = await client.GetAsync("expenses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, queue.Requests.Count);
        Assert.Equal("old-access", queue.Requests[0].Headers.Authorization?.Parameter);
        Assert.Equal("new-access", queue.Requests[1].Headers.Authorization?.Parameter);
        Assert.Equal("new-refresh", store.RefreshToken);
        Assert.Single(refreshQueue.Requests);
    }

    [Fact]
    public async Task Refresh_istegine_bearer_token_gonderilmez()
    {
        var store = new FakeTokenStore();
        await store.SaveAsync(new TokenPair("old-access", "old-refresh"));
        var refreshQueue = RefreshHandler(HttpStatusCode.OK);
        var queue = new QueueHandler(
            QueueHandler.Json(HttpStatusCode.Unauthorized, Problem("Unauthorized", "unauthorized")),
            QueueHandler.Empty(HttpStatusCode.OK));
        using var client = CreateClient(store, refreshQueue, queue);

        await client.GetAsync("expenses");

        Assert.Null(refreshQueue.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task Post_istekleri_otomatik_tekrar_edilmez()
    {
        var store = new FakeTokenStore();
        await store.SaveAsync(new TokenPair("old-access", "old-refresh"));
        var queue = new QueueHandler(
            QueueHandler.Json(HttpStatusCode.Unauthorized, Problem("Unauthorized", "unauthorized")));
        using var client = CreateClient(store, RefreshHandler(HttpStatusCode.OK), queue);

        await client.PostAsJsonAsync("expenses", new { amount = 10m });

        Assert.Single(queue.Requests);
        Assert.Equal("new-access", store.AccessToken);
    }

    [Fact]
    public async Task Refresh_basarisiz_oldugunda_tokenlar_silinir_ve_oturum_olayi_tek_kez_yukseltilir()
    {
        var store = new FakeTokenStore();
        await store.SaveAsync(new TokenPair("old-access", "old-refresh"));
        var queue = new QueueHandler(
            QueueHandler.Json(HttpStatusCode.Unauthorized, Problem("Unauthorized", "unauthorized")));
        var expiredCount = 0;

        var authHandler = new AuthHandler(store, new SessionRefresher(
            new HttpClient(RefreshHandler(HttpStatusCode.Unauthorized)) { BaseAddress = BaseAddress },
            store))
        {
            InnerHandler = queue,
        };
        authHandler.SessionExpired += () =>
        {
            expiredCount++;
            return Task.CompletedTask;
        };
        using var directClient = new HttpClient(authHandler) { BaseAddress = BaseAddress };

        var response = await directClient.GetAsync("expenses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, expiredCount);
        Assert.Equal(1, store.ClearCount);
        Assert.Null(store.AccessToken);
    }

    [Fact]
    public async Task ApiException_alan_hatalarini_kullanici_mesajina_ekler()
    {
        var exception = await ApiException.FromResponseAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new
            {
                type = "https://fintrack.local/errors/validation",
                title = "Doğrulama hatası",
                status = 400,
                code = "ValidationFailed",
                errors = new { amount = new[] { "Tutar sıfırdan büyük olmalıdır." } },
            }, options: ApiJson.Options),
        });

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("ValidationFailed", exception.Code);
        Assert.Contains("Tutar sıfırdan büyük olmalıdır.", exception.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("old-access", exception.UserMessage, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(FakeTokenStore store, HttpMessageHandler refreshHandler, HttpMessageHandler apiHandler)
    {
        var refresher = new SessionRefresher(
            new HttpClient(refreshHandler) { BaseAddress = BaseAddress },
            store);
        var auth = new AuthHandler(store, refresher) { InnerHandler = apiHandler };
        return new HttpClient(auth) { BaseAddress = BaseAddress };
    }

    private static QueueHandler RefreshHandler(HttpStatusCode status) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(
                new
                {
                    userId = Guid.NewGuid(),
                    email = "user@example.com",
                    preferredTimeZone = "Europe/Istanbul",
                    accessToken = "new-access",
                    refreshToken = "new-refresh",
                    expiresIn = 900,
                    tokenType = "Bearer",
                },
                options: ApiJson.Options),
        }));

    private static object Problem(string title, string code) => new
    {
        type = "https://fintrack.local/errors/unauthorized",
        title,
        status = 401,
        code,
        detail = "Oturum süresi doldu.",
    };
}
