using System.Text;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Tests.Fakes;
using Xunit;

namespace FinTrack.Mobile.Tests;

public sealed class PendingOperationStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Aes_gcm_roundtrip_returns_original_body_and_stores_only_cipher()
    {
        using var database = new TempDatabase();
        var secureStore = new FakeSecureKeyValueStore();
        using var store = new PendingOperationStore(database.DatabasePath, secureStore);
        var body = Encoding.UTF8.GetBytes("{\"amount\":10.5,\"currency\":\"TRY\"}");

        var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", body);

        Assert.Equal(12, record.Nonce.Length);
        Assert.Equal(16, record.Tag.Length);
        Assert.False(record.BodyCipher.SequenceEqual(body));
        Assert.Equal(body, await store.TryReadBodyAsync(record));
        var key = Assert.Single(secureStore.Values);
        Assert.Equal(32, Convert.FromBase64String(key.Value).Length);
        Assert.DoesNotContain("amount", key.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rewrite_uses_fresh_nonce_and_tag()
    {
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", Encoding.UTF8.GetBytes("first"));
        var originalNonce = record.Nonce.ToArray();
        var originalTag = record.Tag.ToArray();
        var originalCipher = record.BodyCipher.ToArray();

        var rewritten = await store.RewriteBodyAsync(record.Key, Encoding.UTF8.GetBytes("second"));

        Assert.NotEqual(originalNonce, rewritten.Nonce);
        Assert.NotEqual(originalTag, rewritten.Tag);
        Assert.NotEqual(originalCipher, rewritten.BodyCipher);
        Assert.Equal(Encoding.UTF8.GetBytes("second"), await store.TryReadBodyAsync(rewritten));
    }

    [Fact]
    public async Task Aad_tamper_and_key_loss_prevent_decryption()
    {
        using var database = new TempDatabase();
        var secureStore = new FakeSecureKeyValueStore();
        using var store = new PendingOperationStore(database.DatabasePath, secureStore);
        var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", Encoding.UTF8.GetBytes("body"));

        Assert.Null(await store.TryReadBodyAsync(record with { Route = "/api/v1/tampered" }));
        Assert.Null(await store.TryReadBodyAsync(record with { Method = "PUT" }));
        Assert.Null(await store.TryReadBodyAsync(record with { Key = Guid.NewGuid().ToString("D") }));

        secureStore.Remove(PendingOperationKeyNames.ForUser("user-a"));
        Assert.Null(await store.TryReadBodyAsync(record));
        Assert.NotNull(await store.GetAsync(record.Key));
    }

    [Fact]
    public async Task Records_are_isolated_by_user_and_encryption_key()
    {
        using var database = new TempDatabase();
        var secureStore = new FakeSecureKeyValueStore();
        using var store = new PendingOperationStore(database.DatabasePath, secureStore);
        var body = Encoding.UTF8.GetBytes("same body");
        var first = await store.AddAsync("user-a", "POST", "/api/v1/expenses", body);
        var second = await store.AddAsync("user-b", "POST", "/api/v1/expenses", body);

        Assert.Single(await store.ListForUserAsync("user-a"));
        Assert.Single(await store.ListForUserAsync("user-b"));
        Assert.Equal(first.Key, (await store.ListForUserAsync("user-a"))[0].Key);
        Assert.Equal(second.Key, (await store.ListForUserAsync("user-b"))[0].Key);
        Assert.Equal(2, secureStore.Values.Count);
        Assert.NotEqual(
            PendingOperationKeyNames.ForUser("user-a"),
            PendingOperationKeyNames.ForUser("user-b"));
        Assert.Null(await store.TryReadBodyAsync(first with { UserIdKey = "user-b" }));
        Assert.Equal(body, await store.TryReadBodyAsync(first));
    }

    [Fact]
    public async Task Max_fifty_operations_evicts_oldest_with_warning()
    {
        using var database = new TempDatabase();
        var clock = new FakeTimeProvider(Start);
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore(), clock);
        var evicted = new List<string>();
        store.OperationEvicted += (_, args) => evicted.Add(args.Key);

        string? firstKey = null;
        for (var index = 0; index < store.MaxOperations + 1; index++)
        {
            var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", [1, 2, 3]);
            firstKey ??= record.Key;
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(store.MaxOperations, (await store.ListForUserAsync("user-a")).Count);
        Assert.Null(await store.GetAsync(firstKey!));
        Assert.Equal(firstKey, Assert.Single(evicted));
    }

    [Fact]
    public async Task Body_limit_is_enforced_at_sixty_four_kilobytes()
    {
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());

        await Assert.ThrowsAsync<PendingOperationBodyTooLargeException>(() =>
            store.AddAsync("user-a", "POST", "/api/v1/expenses", new byte[store.MaxBodyBytes + 1]));

        var body = new byte[store.MaxBodyBytes];
        Random.Shared.NextBytes(body);
        var record = await store.AddAsync("user-a", "POST", "/api/v1/expenses", body);
        Assert.Equal(body, await store.TryReadBodyAsync(record));
    }
}
