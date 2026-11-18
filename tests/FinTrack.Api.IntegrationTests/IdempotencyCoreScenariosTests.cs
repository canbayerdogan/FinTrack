using System.Net;
using System.Net.Http.Json;
using FinTrack.Api.Idempotency;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>S1–S3, S7, S9–S12, S14, S15, S18, S20, S25.</summary>
[Collection(ApiCollection.Name)]
public sealed class IdempotencyCoreScenariosTests(ApiFixture fixture) : IdempotencyTestBase(fixture)
{
    [Fact]
    public async Task S1_Ayni_key_ve_govde_ayni_yaniti_verir_tek_kayit_olur()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var body = ExpenseBody(category.Id, 125.5m, "replay-test");

        using var first = await PostWithKeyAsync(client, ExpensesBase, body, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync();
        var firstLocation = first.Headers.Location?.ToString();

        using var second = await PostWithKeyAsync(client, ExpensesBase, body, key);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(firstBody, await second.Content.ReadAsStringAsync());
        Assert.Equal(firstLocation, second.Headers.Location?.ToString());
        Assert.Equal(1, await CountExpensesAsync("replay-test"));
    }

    [Fact]
    public async Task S2_Ayni_key_farkli_govde_409_doner()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();

        using var first = await PostWithKeyAsync(
            client, ExpensesBase, ExpenseBody(category.Id, 100m, "reuse-first"), key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await PostWithKeyAsync(
            client, ExpensesBase, ExpenseBody(category.Id, 200m, "reuse-second"), key);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("IdempotencyKeyReused", await ReadCodeAsync(second));
        Assert.Equal(1, await CountExpensesAsync("reuse-first"));
        Assert.Equal(0, await CountExpensesAsync("reuse-second"));
    }

    [Fact]
    public async Task S3_Ayni_process_eszamanli_isteklerde_biri_2xx_digeri_inprogress()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            BeforeCommitAsync = async () =>
            {
                started.TrySetResult();
                await release.Task;
            },
        });

        try
        {
            var firstTask = PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 75m, "concurrent"), key);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var secondTask = PostWithKeyAsync(client, ExpensesBase, ExpenseBody(category.Id, 75m, "concurrent"), key);
            await Task.Delay(300);
            release.TrySetResult();

            using var first = await firstTask;
            using var second = await secondTask;

            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
                new[] { first.StatusCode, second.StatusCode }.OrderBy(status => status));
            var conflict = first.StatusCode == HttpStatusCode.Conflict ? first : second;
            Assert.Equal("IdempotencyInProgress", await ReadCodeAsync(conflict));
            Assert.Equal(1, await CountExpensesAsync("concurrent"));
        }
        finally
        {
            release.TrySetResult();
            Hooks.Clear(key.ToString("D"));
        }
    }

    [Fact]
    public async Task S7_4xx_sonrasi_ayni_key_ile_yeni_execution_calisir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();

        using var invalid = await PostWithKeyAsync(
            client, ExpensesBase, ExpenseBody(category.Id, 0m, "after-400"), key);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, await CountExpensesAsync("after-400"));

        using var valid = await PostWithKeyAsync(
            client, ExpensesBase, ExpenseBody(category.Id, 50m, "after-400"), key);
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        Assert.Equal(1, await CountExpensesAsync("after-400"));
    }

    [Fact]
    public async Task S9_Farkli_kullanicilar_ayni_keyi_kullanabilir()
    {
        var userA = await CreateAuthenticatedClientAsync();
        var userB = await CreateAuthenticatedClientAsync();
        var key = Guid.NewGuid();
        var categoryA = await CreateCategoryAsync(userA, "KullaniciA");
        var categoryB = await CreateCategoryAsync(userB, "KullaniciB");

        using var first = await PostWithKeyAsync(
            userA, ExpensesBase, ExpenseBody(categoryA.Id, 10m, "user-a"), key);
        using var second = await PostWithKeyAsync(
            userB, ExpensesBase, ExpenseBody(categoryB.Id, 20m, "user-b"), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(1, await CountExpensesAsync("user-a"));
        Assert.Equal(1, await CountExpensesAsync("user-b"));

        using var statusA = await userA.GetAsync($"/api/v1/idempotency/{key}");
        using var statusB = await userB.GetAsync($"/api/v1/idempotency/{key}");
        Assert.Equal(HttpStatusCode.OK, statusA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusB.StatusCode);
    }

    [Fact]
    public async Task S10_Ayni_key_farkli_route_template_ayri_kapsamdir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var key = Guid.NewGuid();

        using var category = await PostWithKeyAsync(
            client, CategoriesBase, new { name = "RouteA", iconKey = "cart" }, key);
        using var card = await PostWithKeyAsync(client, CardsBase, CardBody(), key);

        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        Assert.Equal(HttpStatusCode.Created, card.StatusCode);
        Assert.Equal(1, await CountCategoriesAsync("RouteA"));
    }

    [Fact]
    public async Task S11_Ayni_key_farkli_route_degeri_ayri_kapsamdir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var first = await CreateCategoryAsync(client, "ArchiveA");
        var second = await CreateCategoryAsync(client, "ArchiveB");
        var key = Guid.NewGuid();

        using var firstArchive = await PostWithKeyAsync(
            client, $"{CategoriesBase}/{first.Id}/archive", new { rowVersion = first.RowVersion }, key);
        using var secondArchive = await PostWithKeyAsync(
            client, $"{CategoriesBase}/{second.Id}/archive", new { rowVersion = second.RowVersion }, key);

        Assert.Equal(HttpStatusCode.NoContent, firstArchive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondArchive.StatusCode);
    }

    [Fact]
    public async Task S12_Query_string_scope_a_girmez_replay_olur()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var body = ExpenseBody(category.Id, 30m, "query-test");

        using var first = await PostWithKeyAsync(client, $"{ExpensesBase}?source=one", body, key);
        using var second = await PostWithKeyAsync(client, $"{ExpensesBase}?source=two", body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(1, await CountExpensesAsync("query-test"));
    }

    [Fact]
    public async Task S14_Gecersiz_header_ve_govde_400_doner()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client);

        using var missing = await client.PostAsJsonAsync(ExpensesBase, ExpenseBody(category.Id, 10m, "missing"));
        // Bu istemci otomatik key ekler; eksik header denemesi düz istemciyle yapılır.
        Assert.Equal(HttpStatusCode.Created, missing.StatusCode);

        using var plain = await CreateAuthenticatedClientAsync(addIdempotencyHeaders: false);
        using var noHeader = await plain.PostAsJsonAsync(ExpensesBase, ExpenseBody(category.Id, 10m, "no-header"));
        Assert.Equal(HttpStatusCode.BadRequest, noHeader.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(noHeader));

        using var invalidUuid = await PostRawWithKeyAsync(
            client, ExpensesBase, "{\"amount\":1}", Guid.Empty);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUuid.StatusCode);

        using var brokenJson = await PostRawWithKeyAsync(
            client, ExpensesBase, "{", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, brokenJson.StatusCode);

        using var outOfRange = await PostRawWithKeyAsync(
            client,
            ExpensesBase,
            "{\"amount\":549755813888,\"currency\":\"TRY\",\"occurredOn\":\"2026-01-01\",\"categoryId\":\"" +
            category.Id + "\",\"description\":\"limit\",\"paymentMethod\":\"Cash\"}",
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
    }

    [Fact]
    public async Task S15_Auth_POST_key_tasir_but_idempotency_kaydi_olusturmaz()
    {
        var client = IdempotencyTestClient.CreatePlain(Factory);
        var key = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                email = $"auth-key-{Guid.NewGuid():N}@example.com",
                password = "Test12345",
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));

        using var register = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var auth = await ReadAsync<AuthResponse>(register);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var status = await client.GetAsync($"/api/v1/idempotency/{key}");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(0, await CountIdempotencyKeysAsync(key));
    }

    [Fact]
    public async Task S20_64kb_ustu_2xx_yanti_kayit_olusturmaz()
    {
        var client = await CreateAuthenticatedClientAsync();
        var key = Guid.NewGuid();
        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            ResponseBodyOverride = new string('x', 70 * 1024),
        });

        try
        {
            using var response = await PostWithKeyAsync(
                client, CategoriesBase, new { name = "Oversized", iconKey = "cart" }, key);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("InternalError", await ReadCodeAsync(response));
            Assert.Equal(0, await CountCategoriesAsync("Oversized"));
            Assert.Equal(0, await CountIdempotencyKeysAsync(key));
        }
        finally
        {
            Hooks.Clear(key.ToString("D"));
        }
    }

    [Fact]
    public async Task S18_Nakit_avansi_ve_para_girisi_tek_transactionda_atomiktir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var card = await CreateCardAsync(client);
        var key = Guid.NewGuid();
        var body = new { amount = 750m, currency = "TRY", takenOn = Past, description = "advance-atomic" };

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            CommitMode = IdempotencyTestCommitMode.DuringCommit,
        });

        try
        {
            using var failed = await PostWithKeyAsync(
                client, $"{CardsBase}/{card.Id}/cash-advances", body, key);
            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM cards.CashAdvances WHERE Description = @p0", "advance-atomic"));
            Assert.Equal(0, await CountInflowsAsync("advance-atomic"));
        }
        finally
        {
            Hooks.Clear(key.ToString("D"));
        }

        using var success = await PostWithKeyAsync(
            client, $"{CardsBase}/{card.Id}/cash-advances", body, key);
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM cards.CashAdvances WHERE Description = @p0", "advance-atomic"));
        Assert.Equal(1, await CountInflowsAsync("advance-atomic"));
    }

    [Fact]
    public async Task S25_Dort_modul_de_host_scope_una_katilir()
    {
        var client = await CreateAuthenticatedClientAsync();
        var category = await CreateCategoryAsync(client, "DortModul");

        using var expense = await PostWithKeyAsync(
            client, ExpensesBase, ExpenseBody(category.Id, 10m, "module-expense"), Guid.NewGuid());
        using var inflow = await PostWithKeyAsync(
            client,
            InflowsBase,
            new
            {
                amount = 20m,
                currency = "TRY",
                occurredOn = Past,
                source = "Salary",
                description = "module-inflow",
            },
            Guid.NewGuid());
        using var card = await PostWithKeyAsync(client, CardsBase, CardBody(), Guid.NewGuid());
        using var debt = await PostWithKeyAsync(
            client,
            DebtsBase,
            new
            {
                counterpartyName = "Ali",
                principal = 30m,
                borrowedOn = Past,
                dueDate = Past.AddDays(30),
                description = (string?)null,
                currency = "TRY",
            },
            Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);
        Assert.Equal(HttpStatusCode.Created, inflow.StatusCode);
        Assert.Equal(HttpStatusCode.Created, card.StatusCode);
        Assert.Equal(HttpStatusCode.Created, debt.StatusCode);
        Assert.Equal(1, await CountExpensesAsync("module-expense"));
        Assert.Equal(1, await CountInflowsAsync("module-inflow"));
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

    private static object CardBody() => new
    {
        bankName = "Örnek Bank",
        displayName = "Platinum",
        logoKey = "ornek-bank",
        creditLimit = 50_000m,
        statementDay = 25,
        paymentDueDay = 5,
    };
}
