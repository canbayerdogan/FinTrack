using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Api.Idempotency;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>S5, S6, S8, S17, S21, S28, S29.</summary>
[Collection(ApiCollection.Name)]
public sealed class IdempotencyFailureAndLifecycleTests(ApiFixture fixture) : IdempotencyTestBase(fixture)
{
    [Fact]
    public async Task S5_SaveChanges_sonrasi_commit_oncesi_cokme_rollback_olur()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var description = "fail-before-commit";

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            CommitMode = IdempotencyTestCommitMode.BeforeCommit,
        });

        try
        {
            using var failed = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
            Assert.Equal("IdempotencyInProgress", await ReadCodeAsync(failed));
            Assert.Equal(0, await CountExpensesAsync(description));
            Assert.Equal(0, await CountIdempotencyKeysAsync(key));
        }
        finally
        {
            Hooks.Clear(key.ToString("D"));
        }

        using var retry = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, await CountExpensesAsync(description));
    }

    [Fact]
    public async Task S6_Commit_sonrasi_yanit_yazilmadan_cokme_replay_edilir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var description = "fail-after-commit";

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            CommitMode = IdempotencyTestCommitMode.AfterCommit,
        });

        try
        {
            using var failed = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
            Assert.Equal("IdempotencyInProgress", await ReadCodeAsync(failed));
            Assert.Equal(1, await CountExpensesAsync(description));
            Assert.Equal("Completed", await ReadStatusAsync(key));
        }
        finally
        {
            Hooks.Clear(key.ToString("D"));
        }

        using var retry = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Contains(description, await retry.Content.ReadAsStringAsync());
        Assert.NotNull(retry.Headers.Location);
        Assert.Equal(1, await CountExpensesAsync(description));
    }

    [Fact]
    public async Task S21_Commit_sirasinda_baglanti_kopmasi_ayni_key_ile_tek_kayit_verir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var description = "ambiguous-commit";

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            CommitMode = IdempotencyTestCommitMode.DuringCommit,
        });

        try
        {
            using var uncertain = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            Assert.Equal(HttpStatusCode.Conflict, uncertain.StatusCode);
            Assert.Equal("IdempotencyInProgress", await ReadCodeAsync(uncertain));
        }
        finally
        {
            Hooks.Clear(key.ToString("D"));
        }

        using var replayOrExecute = await PostWithKeyAsync(
            client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Created, replayOrExecute.StatusCode);
        Assert.Equal(1, await CountExpensesAsync(description));
        Assert.Equal(1, await CountIdempotencyKeysAsync(key));
    }

    [Fact]
    public async Task S8_S28_24_saat_sonra_tombstone_ve_durum_sorgusu_calisir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var other = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var description = "expired-body";

        using var create = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var liveStatus = await client.GetAsync($"/api/v1/idempotency/{key}");
        Assert.Equal(HttpStatusCode.OK, liveStatus.StatusCode);
        using var liveJson = JsonDocument.Parse(await liveStatus.Content.ReadAsStringAsync());
        Assert.Equal("Completed", liveJson.RootElement.GetProperty("status").GetString());
        Assert.True(liveJson.RootElement.GetProperty("hasBody").GetBoolean());
        Assert.False(liveJson.RootElement.TryGetProperty("body", out _));

        Assert.Equal(1, await ExecuteAsync(
            "UPDATE shared.IdempotencyKeys SET ExpiresAtUtc = DATEADD(DAY, -1, SYSUTCDATETIME()) WHERE [Key] = @p0",
            key.ToString("D")));

        using var expired = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        Assert.Equal("IdempotencyExpired", await ReadCodeAsync(expired));
        Assert.Equal(1, await CountExpensesAsync(description));
        Assert.Equal("Tombstone", await ReadStatusAsync(key));
        Assert.Equal(0, await CountAsync(
            "SELECT COUNT(*) FROM shared.IdempotencyKeys WHERE [Key] = @p0 AND ResponseBody IS NOT NULL",
            key.ToString("D")));

        using var tombstoneStatus = await client.GetAsync($"/api/v1/idempotency/{key}");
        Assert.Equal(HttpStatusCode.OK, tombstoneStatus.StatusCode);
        using var tombstoneJson = JsonDocument.Parse(await tombstoneStatus.Content.ReadAsStringAsync());
        Assert.Equal("Tombstone", tombstoneJson.RootElement.GetProperty("status").GetString());
        Assert.False(tombstoneJson.RootElement.GetProperty("hasBody").GetBoolean());

        using var otherStatus = await other.GetAsync($"/api/v1/idempotency/{key}");
        Assert.Equal(HttpStatusCode.NotFound, otherStatus.StatusCode);
    }

    [Fact]
    public async Task S29_30_gun_sonrasi_durum_bilinmiyor_ve_otomatik_yeni_islem_yok()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var description = "uncertain-after-30d";

        using var create = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        Assert.Equal(1, await ExecuteAsync(
            """
            UPDATE shared.IdempotencyKeys
            SET Status = 3,
                ResponseStatusCode = NULL,
                ResponseContentType = NULL,
                ResponseHeaders = NULL,
                ResponseBody = NULL
            WHERE [Key] = @p0
            """,
            key.ToString("D")));

        using var replay = await PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal("IdempotencyExpired", await ReadCodeAsync(replay));
        Assert.Equal(1, await CountExpensesAsync(description));

        using var deletedStatus = await client.GetAsync($"/api/v1/idempotency/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, deletedStatus.StatusCode);
        Assert.Equal(1, await CountExpensesAsync(description));
    }

    [Fact]
    public async Task S17_Temizlik_en_fazla_1000_satirla_sinirlidir()
    {
        using var secondary = Fixture.CreateFactory();
        var client = await CreateAuthenticatedClientAsync(secondary);
        var cleanup = secondary.Services.GetRequiredService<FinTrack.Api.Idempotency.IIdempotencyCleanup>();
        cleanup.ResetForTests();

        var runId = Guid.NewGuid();
        var scopeHash = IdempotencyScope.Sha256(runId.ToString("D"));
        var keys = await InsertExpiredRowsAsync(scopeHash, 1500);
        Assert.Equal(1500, await CountAsync(
            "SELECT COUNT(*) FROM shared.IdempotencyKeys WHERE ScopeHash = @p0", scopeHash));

        using var trigger = await client.PostAsJsonAsync(CategoriesBase, new { name = "CleanupTrigger", iconKey = "cart" });
        Assert.Equal(HttpStatusCode.Created, trigger.StatusCode);

        var tombstoned = await CountAsync(
            "SELECT COUNT(*) FROM shared.IdempotencyKeys WHERE ScopeHash = @p0 AND Status = 3", scopeHash);
        var stillCompleted = await CountAsync(
            "SELECT COUNT(*) FROM shared.IdempotencyKeys WHERE ScopeHash = @p0 AND Status = 2", scopeHash);

        Assert.Equal(1000, tombstoned);
        Assert.Equal(500, stillCompleted);
        Assert.Equal(1500, keys.Count);
    }

    private async Task<List<Guid>> InsertExpiredRowsAsync(string scopeHash, int count)
    {
        var keys = new List<Guid>(count);
        var table = new DataTable();
        table.Columns.Add("ScopeHash", typeof(string));
        table.Columns.Add("UserHash", typeof(string));
        table.Columns.Add("Key", typeof(string));
        table.Columns.Add("RequestHash", typeof(string));
        table.Columns.Add("Status", typeof(byte));
        table.Columns.Add("RequestContentType", typeof(string));
        table.Columns.Add("ResponseStatusCode", typeof(int));
        table.Columns.Add("ResponseContentType", typeof(string));
        table.Columns.Add("ResponseHeaders", typeof(string));
        table.Columns.Add("ResponseBody", typeof(string));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));
        table.Columns.Add("CompletedAtUtc", typeof(DateTime));
        table.Columns.Add("ExpiresAtUtc", typeof(DateTime));
        table.Columns.Add("TombstoneExpiresAtUtc", typeof(DateTime));

        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            var key = Guid.NewGuid();
            keys.Add(key);
            table.Rows.Add(
                scopeHash,
                IdempotencyScope.Sha256($"cleanup-user-{i}"),
                key.ToString("D"),
                IdempotencyScope.Sha256($"request-{key}"),
                (byte)2,
                "application/json",
                200,
                "application/json; charset=utf-8",
                DBNull.Value,
                "{}",
                now.AddHours(-25),
                now.AddHours(-25),
                now.AddHours(-1),
                now.AddDays(29));
        }

        await using var connection = new SqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        using var bulk = new SqlBulkCopy(connection)
        {
            DestinationTableName = "shared.IdempotencyKeys",
        };
        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulk.WriteToServerAsync(table);
        return keys;
    }

    private async Task<string?> ReadStatusAsync(Guid key)
    {
        var value = await CountAsync(
            "SELECT COUNT(*) FROM shared.IdempotencyKeys WHERE [Key] = @p0 AND Status = 2",
            key.ToString("D"));
        return value == 1 ? "Completed" : "Tombstone";
    }

    private static async Task<string> firstFailedBodyAsync(HttpResponseMessage response)
    {
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("detail", out var detail)
            ? detail.GetString() ?? string.Empty
            : string.Empty;
    }

    private static object ExpenseBody(Guid categoryId, decimal amount, string description) => new
    {
        amount,
        currency = "TRY",
        occurredOn = Past,
        categoryId,
        description,
        paymentMethod = "Cash",
        cardId = (Guid?)null,
    };
}
