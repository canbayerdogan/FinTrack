using System.Net;
using System.Text;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Tests.Fakes;
using Xunit;

namespace FinTrack.Mobile.Tests;

public sealed class AuthHandlerPendingOperationTests
{
    private static readonly Uri BaseAddress = new("https://api.test/api/v1/");
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"amount\":10.5}");

    [Fact]
    public async Task Sent_unconfirmed_post_is_retried_with_identical_key_and_body_bytes()
    {
        using var database = new TempDatabase();
        var tokens = new FakeTokenStore();
        await tokens.SaveAsync(new TokenPair("old-access", "old-refresh"));
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", Body);
        await store.SetStateAsync(record.Key, PendingOperationState.SentUnconfirmed);
        var queue = new QueueHandler(Unauthorized(), QueueHandler.Empty(HttpStatusCode.OK));
        using var client = CreateClient(tokens, store, queue);

        using var request = new HttpRequestMessage(HttpMethod.Post, "expenses")
        {
            Content = new ByteArrayContent(Body),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", record.Key);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, queue.Requests.Count);
        Assert.Equal(Body, await queue.Requests[0].Content!.ReadAsByteArrayAsync());
        Assert.Equal(Body, await queue.Requests[1].Content!.ReadAsByteArrayAsync());
        Assert.Equal(record.Key, queue.Requests[0].Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal(record.Key, queue.Requests[1].Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("new-access", queue.Requests[1].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Post_without_sent_unconfirmed_record_is_not_retried()
    {
        using var database = new TempDatabase();
        var tokens = new FakeTokenStore();
        await tokens.SaveAsync(new TokenPair("old-access", "old-refresh"));
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());

        foreach (var state in new PendingOperationState?[] { null, PendingOperationState.Created, PendingOperationState.Uncertain })
        {
            var key = Guid.NewGuid().ToString("D");
            if (state.HasValue)
            {
                var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", Body);
                key = record.Key;
                await store.SetStateAsync(key, state.Value);
            }

            var queue = new QueueHandler(Unauthorized(), QueueHandler.Empty(HttpStatusCode.OK));
            using var client = CreateClient(tokens, store, queue);
            using var request = new HttpRequestMessage(HttpMethod.Post, "expenses")
            {
                Content = new ByteArrayContent(Body),
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Single(queue.Requests);
        }
    }

    [Fact]
    public async Task Auth_posts_and_put_are_never_retried()
    {
        using var database = new TempDatabase();
        var tokens = new FakeTokenStore();
        await tokens.SaveAsync(new TokenPair("old-access", "old-refresh"));
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", Body);
        await store.SetStateAsync(record.Key, PendingOperationState.SentUnconfirmed);

        foreach (var (method, route) in new[]
                 {
                     (HttpMethod.Post, "auth/login"),
                     (HttpMethod.Put, $"expenses/{Guid.NewGuid():D}"),
                 })
        {
            var queue = new QueueHandler(Unauthorized(), QueueHandler.Empty(HttpStatusCode.OK));
            using var client = CreateClient(tokens, store, queue);
            using var request = new HttpRequestMessage(method, route)
            {
                Content = new ByteArrayContent(Body),
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", record.Key);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Single(queue.Requests);
        }
    }

    private static HttpClient CreateClient(FakeTokenStore tokens, IPendingOperationStore store, HttpMessageHandler api)
    {
        var refresher = new SessionRefresher(
            new HttpClient(new QueueHandler(QueueHandler.Json(
                HttpStatusCode.OK,
                new
                {
                    userId = Guid.NewGuid(),
                    email = "user@example.com",
                    preferredTimeZone = "Europe/Istanbul",
                    accessToken = "new-access",
                    refreshToken = "new-refresh",
                    expiresIn = 900,
                    tokenType = "Bearer",
                })))
            {
                BaseAddress = BaseAddress,
            },
            tokens);
        var auth = new AuthHandler(tokens, refresher, store) { InnerHandler = api };
        return new HttpClient(auth) { BaseAddress = BaseAddress };
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Unauthorized() =>
        QueueHandler.Json(
            HttpStatusCode.Unauthorized,
            new
            {
                status = 401,
                title = "Oturum süresi doldu",
                code = "Unauthorized",
                detail = "Oturum süresi doldu.",
            });
}
