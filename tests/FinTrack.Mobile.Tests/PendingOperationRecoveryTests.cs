using System.Net;
using System.Text;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FinTrack.Mobile.Tests;

public sealed class PendingOperationRecoveryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task S16_restart_replays_same_key_and_body()
    {
        using var database = new TempDatabase();
        var secureStore = new FakeSecureKeyValueStore();
        var clock = new FakeTimeProvider(Start);
        var body = Encoding.UTF8.GetBytes("{\"amount\":10.5}");
        var queue = new QueueHandler(QueueHandler.Json(HttpStatusCode.Created, new { id = Guid.NewGuid() }));
        PendingOperation record;

        using (var firstStore = new PendingOperationStore(database.DatabasePath, secureStore, clock))
        {
            var firstRecovery = new PendingOperationRecoveryService(CreateHttp(queue), firstStore, clock);
            record = await firstRecovery.CreateAsync("user-a", "POST", "/api/v1/expenses", body);
            // Yanıt alınmadan önce çökme simülasyonu: kayıt SentUnconfirmed olarak kaldı.
            await firstStore.SetStateAsync(record.Key, PendingOperationState.SentUnconfirmed);
        }

        using var restartedStore = new PendingOperationStore(database.DatabasePath, secureStore, clock);
        var restartedRecovery = new PendingOperationRecoveryService(CreateHttp(queue), restartedStore, clock);

        var report = await restartedRecovery.RecoverAsync("user-a");

        var result = Assert.Single(report.Results);
        Assert.Equal(RecoveryStatus.Completed, result.Status);
        Assert.Single(queue.Requests);
        Assert.Equal(HttpMethod.Post, queue.Requests[0].Method);
        Assert.Equal(record.Key, queue.Requests[0].Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal(body, Assert.Single(queue.Bodies));
        Assert.Null(await restartedStore.GetAsync(record.Key));
    }

    [Fact]
    public async Task Created_records_are_never_auto_sent()
    {
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var recovery = new PendingOperationRecoveryService(CreateHttp(new QueueHandler()), store);
        var record = await recovery.CreateAsync("user-a", "POST", "/api/v1/expenses", [1, 2, 3]);

        var report = await recovery.RecoverAsync("user-a");

        Assert.Empty(report.Results);
        var stored = await store.GetAsync(record.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.Created, stored!.State);
    }

    [Fact]
    public async Task Definitive_key_reused_deletes_record()
    {
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var queue = new QueueHandler(QueueHandler.Json(
            HttpStatusCode.Conflict,
            new
            {
                status = 409,
                code = "IdempotencyKeyReused",
                title = "Anahtar yeniden kullanıldı",
                detail = "Farklı gövde.",
            }));
        var recovery = new PendingOperationRecoveryService(CreateHttp(queue), store);
        var record = await CreateSentUnconfirmedAsync(recovery, store);

        var result = await recovery.RecoverOneAsync(record.Key);

        Assert.Equal(RecoveryStatus.DefiniteFailure, result.Status);
        Assert.Null(await store.GetAsync(record.Key));
        Assert.Single(queue.Requests);
    }

    [Fact]
    public async Task Over_twenty_four_hours_with_404_marks_uncertain_without_new_uuid()
    {
        using var database = new TempDatabase();
        var clock = new FakeTimeProvider(Start);
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore(), clock);
        var queue = new QueueHandler(QueueHandler.Empty(HttpStatusCode.NotFound));
        var recovery = new PendingOperationRecoveryService(CreateHttp(queue), store, clock);
        var record = await CreateSentUnconfirmedAsync(recovery, store);
        clock.Advance(TimeSpan.FromHours(25));

        var result = await recovery.RecoverOneAsync(record.Key);

        Assert.Equal(RecoveryStatus.Uncertain, result.Status);
        Assert.Contains("kayıtlarını kontrol", result.Message, StringComparison.Ordinal);
        Assert.Equal(PendingOperationRecoveryService.UnknownResultMessage, recovery.LastMessage);
        Assert.Single(queue.Requests);
        Assert.Equal(HttpMethod.Get, queue.Requests[0].Method);
        Assert.EndsWith($"idempotency/{record.Key}", queue.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        var stored = await store.GetAsync(record.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.Uncertain, stored!.State);
        Assert.Equal(record.Key, Assert.Single(await store.ListForUserAsync("user-a")).Key);
    }

    [Fact]
    public async Task Status_query_completed_is_definitive_success_and_deletes_record()
    {
        using var database = new TempDatabase();
        var clock = new FakeTimeProvider(Start);
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore(), clock);
        var queue = new QueueHandler(QueueHandler.Json(
            HttpStatusCode.OK,
            new { status = "Completed", completedAtUtc = Start, hasBody = false }));
        var recovery = new PendingOperationRecoveryService(CreateHttp(queue), store, clock);
        var record = await CreateSentUnconfirmedAsync(recovery, store);
        clock.Advance(TimeSpan.FromHours(25));

        var result = await recovery.RecoverOneAsync(record.Key);

        Assert.Equal(RecoveryStatus.Completed, result.Status);
        Assert.Null(await store.GetAsync(record.Key));
        Assert.Single(queue.Requests);
        Assert.Equal(HttpMethod.Get, queue.Requests[0].Method);
    }

    [Fact]
    public async Task Aad_tamper_marks_record_uncertain_without_deleting_or_resending()
    {
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var queue = new QueueHandler();
        var recovery = new PendingOperationRecoveryService(CreateHttp(queue), store);
        var record = await CreateSentUnconfirmedAsync(recovery, store);

        using (var connection = new SqliteConnection($"Data Source={database.DatabasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE \"PendingOperations\" SET \"Route\" = $route WHERE \"Key\" = $key;";
            command.Parameters.AddWithValue("$route", "/api/v1/tampered");
            command.Parameters.AddWithValue("$key", record.Key);
            command.ExecuteNonQuery();
        }

        var result = await recovery.RecoverOneAsync(record.Key);

        Assert.Equal(RecoveryStatus.Uncertain, result.Status);
        Assert.Equal(PendingOperationRecoveryService.UnknownResultMessage, result.Message);
        Assert.Empty(queue.Requests);
        var stored = await store.GetAsync(record.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.Uncertain, stored!.State);
    }

    [Fact]
    public async Task Network_failures_keep_sent_unconfirmed_with_bounded_retries()
    {
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var delays = new List<TimeSpan>();
        var queue = new QueueHandler(
            (_, _) => throw new HttpRequestException("offline"),
            (_, _) => throw new HttpRequestException("offline"),
            (_, _) => throw new HttpRequestException("offline"));
        var recovery = new PendingOperationRecoveryService(
            CreateHttp(queue),
            store,
            delay: (duration, _) =>
            {
                delays.Add(duration);
                return Task.CompletedTask;
            });
        var record = await CreateSentUnconfirmedAsync(recovery, store);

        var result = await recovery.RecoverOneAsync(record.Key);

        Assert.Equal(RecoveryStatus.Pending, result.Status);
        Assert.Equal(3, queue.Requests.Count);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) }, delays);
        var stored = await store.GetAsync(record.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.SentUnconfirmed, stored!.State);
        Assert.Equal(3, stored.AttemptCount);
    }

    private static HttpClient CreateHttp(QueueHandler queue) =>
        new(queue)
        {
            BaseAddress = new Uri("https://api.test/api/v1/"),
        };

    private static async Task<PendingOperation> CreateSentUnconfirmedAsync(
        PendingOperationRecoveryService recovery,
        IPendingOperationStore store)
    {
        var record = await recovery.CreateAsync(
            "user-a",
            "POST",
            "/api/v1/expenses",
            Encoding.UTF8.GetBytes("{\"amount\":1}"));
        await store.SetStateAsync(record.Key, PendingOperationState.SentUnconfirmed);
        return record;
    }
}
