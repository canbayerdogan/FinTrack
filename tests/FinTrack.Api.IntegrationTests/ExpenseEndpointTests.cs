using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Harcama/kategori sözleşmesi (docs/06 §3–4) ve docs/07 slice 2 kabul testleri.
/// Her test kendi kullanıcısını üretir; testler sıraya bağımlı değildir.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ExpenseEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string CategoriesBase = "/api/v1/categories";
    private const string ExpensesBase = "/api/v1/expenses";
    private const string CardsBase = "/api/v1/cards";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
    private static readonly DateOnly Past = Today.AddDays(-1);

    [Fact]
    public async Task Kategoriler_yasam_dongusu_ve_arsivleme()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var category = await CreateCategoryAsync(client, "Market");
        Assert.Equal("Market", category.Name);
        Assert.Equal("cart", category.IconKey);
        Assert.False(category.IsArchived);
        Assert.Equal(8, Convert.FromBase64String(category.RowVersion).Length);

        var list = await client.GetFromJsonAsync<List<CategoryResponse>>(CategoriesBase);
        Assert.NotNull(list);
        Assert.Single(list!);

        var duplicate = await client.PostAsJsonAsync(CategoriesBase, new { name = " market ", iconKey = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("StateConflict", await ReadCodeAsync(duplicate));

        var update = await client.PutAsJsonAsync($"{CategoriesBase}/{category.Id}", new
        {
            name = "Gıda",
            iconKey = "cart",
            rowVersion = category.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Gıda", updated!.Name);
        Assert.NotEqual(category.RowVersion, updated.RowVersion);

        var archive = await client.PostAsJsonAsync($"{CategoriesBase}/{category.Id}/archive", new
        {
            rowVersion = updated.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var defaultList = await client.GetFromJsonAsync<List<CategoryResponse>>(CategoriesBase);
        Assert.NotNull(defaultList);
        Assert.Empty(defaultList!);

        var allList = await client.GetFromJsonAsync<List<CategoryResponse>>($"{CategoriesBase}?includeArchived=true");
        Assert.NotNull(allList);
        var archived = Assert.Single(allList!);
        Assert.True(archived.IsArchived);

        // Arşivli kategorinin adı/ikonu güncellenebilir.
        var renameArchived = await client.PutAsJsonAsync($"{CategoriesBase}/{category.Id}", new
        {
            name = "Gıda 2",
            iconKey = "cart",
            rowVersion = archived.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, renameArchived.StatusCode);
    }

    [Fact]
    public async Task Arsivli_kategoriye_harcama_eklenemez()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);

        var archive = await client.PostAsJsonAsync($"{CategoriesBase}/{category.Id}/archive", new
        {
            rowVersion = category.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var response = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount = 100m,
            currency = "TRY",
            occurredOn = Past,
            categoryId = category.Id,
            description = "test",
            paymentMethod = "Cash",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, "categoryId"));
    }

    [Fact]
    public async Task Harcama_olusturma_getirme_guncelleme_ve_iptal()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);

        var create = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount = 1250.5m,
            currency = "try",
            occurredOn = Past,
            categoryId = category.Id,
            description = "Haftalık alışveriş",
            paymentMethod = "Cash",
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var expense = await create.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(expense);
        Assert.Equal(ExpenseBase(create), $"{ExpensesBase}/{expense!.Id}");
        Assert.Equal("TRY", expense.Currency);
        Assert.Equal("Market", expense.CategoryName);
        Assert.Equal("Cash", expense.PaymentMethod);
        Assert.Equal("Active", expense.Status);
        Assert.Equal(0m, expense.RefundedAmount);
        Assert.Equal(1250.5m, expense.NetAmount);
        Assert.NotEqual(default, expense.RecordedAtUtc);

        var fetched = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(expense.Id, fetched!.Id);
        Assert.Equal(expense.RowVersion, fetched.RowVersion);

        var recordedAtRejected = await client.PutAsJsonAsync($"{ExpensesBase}/{expense.Id}", new
        {
            amount = 1300m,
            currency = "TRY",
            occurredOn = Past,
            categoryId = category.Id,
            description = "yeni",
            paymentMethod = "Cash",
            rowVersion = expense.RowVersion,
            recordedAtUtc = DateTime.UtcNow,
        });
        Assert.Equal(HttpStatusCode.BadRequest, recordedAtRejected.StatusCode);
        Assert.True(await HasFieldErrorAsync(recordedAtRejected, "recordedAtUtc"));

        var update = await client.PutAsJsonAsync($"{ExpensesBase}/{expense.Id}", new
        {
            amount = 1300m,
            currency = "TRY",
            occurredOn = Past,
            categoryId = category.Id,
            description = "yeni",
            paymentMethod = "Cash",
            rowVersion = expense.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(updated);
        Assert.Equal(1300m, updated!.Amount);
        Assert.NotEqual(expense.RowVersion, updated.RowVersion);

        var stale = await client.PutAsJsonAsync($"{ExpensesBase}/{expense.Id}", new
        {
            amount = 1400m,
            currency = "TRY",
            occurredOn = Past,
            categoryId = category.Id,
            description = "stale",
            paymentMethod = "Cash",
            rowVersion = expense.RowVersion,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(stale));

        var cancel = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/cancel", new
        {
            reason = "Yanlış kayıt",
            rowVersion = updated.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var cancelled = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);

        var cancelAgain = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/cancel", new
        {
            reason = "Yanlış kayıt",
            rowVersion = cancelled.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, cancelAgain.StatusCode);

        var defaultList = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}");
        Assert.NotNull(defaultList);
        Assert.Equal(0, defaultList!.TotalCount);

        var withCancelled = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?includeCancelled=true");
        Assert.NotNull(withCancelled);
        Assert.Equal(1, withCancelled!.TotalCount);

        var cancelledOnly = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?status=Cancelled");
        Assert.NotNull(cancelledOnly);
        Assert.Equal(1, cancelledOnly!.TotalCount);

        var activeOnly = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?status=Active");
        Assert.NotNull(activeOnly);
        Assert.Equal(0, activeOnly!.TotalCount);
    }

    [Fact]
    public async Task Liste_filtreleri_ve_sayfalama_calisir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);
        var card = await CreateCardAsync(client);

        await CreateExpenseAsync(client, category.Id, amount: 10m, occurredOn: Past.AddDays(-1));
        await CreateExpenseAsync(client, category.Id, amount: 20m, occurredOn: Past);
        await CreateExpenseAsync(
            client,
            category.Id,
            amount: 30m,
            occurredOn: Past,
            paymentMethod: "CreditCard",
            cardId: card.Id);

        var byPaymentMethod = await client.GetFromJsonAsync<PagedResponse>(
            $"{ExpensesBase}?paymentMethod=CreditCard");
        Assert.NotNull(byPaymentMethod);
        Assert.Equal(1, byPaymentMethod!.TotalCount);
        Assert.Equal("CreditCard", byPaymentMethod.Items[0].PaymentMethod);

        var byCard = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?cardId={card.Id}");
        Assert.NotNull(byCard);
        Assert.Equal(1, byCard!.TotalCount);

        var byCategory = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?categoryId={category.Id}");
        Assert.NotNull(byCategory);
        Assert.Equal(3, byCategory!.TotalCount);

        var fromPast = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?from={Past:yyyy-MM-dd}");
        Assert.NotNull(fromPast);
        Assert.Equal(2, fromPast!.TotalCount);

        var toBefore = await client.GetFromJsonAsync<PagedResponse>(
            $"{ExpensesBase}?to={Past.AddDays(-1):yyyy-MM-dd}");
        Assert.NotNull(toBefore);
        Assert.Equal(1, toBefore!.TotalCount);

        var secondPage = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?page=2&pageSize=1");
        Assert.NotNull(secondPage);
        Assert.Equal(3, secondPage!.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.Equal(3, secondPage.TotalPages);

        await AssertValidationFieldAsync(client, $"{ExpensesBase}?paymentMethod=Kripto", "paymentMethod");
        await AssertValidationFieldAsync(client, $"{ExpensesBase}?page=0", "page");
        await AssertValidationFieldAsync(client, $"{ExpensesBase}?pageSize=201", "pageSize");
        await AssertValidationFieldAsync(
            client,
            $"{ExpensesBase}?from={Past:yyyy-MM-dd}&to={Past.AddDays(-2):yyyy-MM-dd}",
            "from");
    }

    [Fact]
    public async Task Kart_alisverisi_tek_kayit_ve_kart_dogrulamasi()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);
        var activeCard = await CreateCardAsync(client);
        var archivedCard = await CreateCardAsync(client, "Arşivli Kart");
        await ArchiveCardAsync(client, archivedCard);

        var archived = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount = 100m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "CreditCard",
            cardId = archivedCard.Id,
        });
        Assert.Equal(HttpStatusCode.BadRequest, archived.StatusCode);
        Assert.True(await HasFieldErrorAsync(archived, "cardId"));

        var unknown = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount = 100m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "CreditCard",
            cardId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.True(await HasFieldErrorAsync(unknown, "cardId"));

        var cardExpense = await CreateExpenseAsync(
            client,
            category.Id,
            amount: 100m,
            paymentMethod: "CreditCard",
            cardId: activeCard.Id);
        Assert.Equal(activeCard.Id, cardExpense.CardId);

        var list = await client.GetFromJsonAsync<PagedResponse>(ExpensesBase);
        Assert.NotNull(list);
        Assert.Equal(1, list!.TotalCount);

        var cardNotAllowed = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount = 100m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "Cash",
            cardId = activeCard.Id,
        });
        Assert.Equal(HttpStatusCode.BadRequest, cardNotAllowed.StatusCode);
        Assert.True(await HasFieldErrorAsync(cardNotAllowed, "cardId"));

        var cleared = await client.PutAsJsonAsync($"{ExpensesBase}/{cardExpense.Id}", new
        {
            amount = 100m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "Cash",
            cardId = (Guid?)null,
            rowVersion = cardExpense.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var updated = await cleared.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(updated);
        Assert.Null(updated!.CardId);
        Assert.Equal("Cash", updated.PaymentMethod);
    }

    [Fact]
    public async Task Iade_yasam_dongusu_ve_net_tutar()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);
        var expense = await CreateExpenseAsync(client, category.Id, amount: 100m);

        var partial = await AddRefundAsync(client, expense.Id, 40m, Past, "Kısmi iade");
        Assert.Equal("Active", partial.Status);
        Assert.Equal(40m, partial.Amount);

        var afterPartial = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(afterPartial);
        Assert.Equal("PartiallyRefunded", afterPartial!.Status);
        Assert.Equal(40m, afterPartial.RefundedAmount);
        Assert.Equal(60m, afterPartial.NetAmount);

        var full = await AddRefundAsync(client, expense.Id, 60m, Past, null);
        Assert.Equal("Active", full.Status);

        var afterFull = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(afterFull);
        Assert.Equal("Refunded", afterFull!.Status);
        Assert.Equal(0m, afterFull.NetAmount);

        var tooMuch = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/refunds", new
        {
            amount = 10m,
            refundedOn = Past,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal("BusinessRuleViolation", await ReadCodeAsync(tooMuch));
        Assert.True(await HasFieldErrorAsync(tooMuch, "amount"));

        var beforeExpense = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/refunds", new
        {
            amount = 10m,
            refundedOn = Past.AddDays(-1),
        });
        Assert.Equal(HttpStatusCode.BadRequest, beforeExpense.StatusCode);
        Assert.True(await HasFieldErrorAsync(beforeExpense, "refundedOn"));

        var future = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/refunds", new
        {
            amount = 10m,
            refundedOn = Today.AddDays(1),
        });
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.True(await HasFieldErrorAsync(future, "refundedOn"));

        var refunds = await client.GetFromJsonAsync<List<RefundResponse>>(
            $"{ExpensesBase}/{expense.Id}/refunds");
        Assert.NotNull(refunds);
        Assert.Equal(2, refunds!.Count);

        var firstRefund = refunds.Single(refund => refund.Amount == 40m);
        var cancelRefund = await client.PostAsJsonAsync(
            $"{ExpensesBase}/{expense.Id}/refunds/{firstRefund.Id}/cancel",
            new { reason = "Müşteri vazgeçti", rowVersion = firstRefund.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelRefund.StatusCode);

        var afterCancelRefund = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(afterCancelRefund);
        Assert.Equal("PartiallyRefunded", afterCancelRefund!.Status);
        Assert.Equal(40m, afterCancelRefund.NetAmount);

        var refundsAfter = await client.GetFromJsonAsync<List<RefundResponse>>(
            $"{ExpensesBase}/{expense.Id}/refunds");
        Assert.NotNull(refundsAfter);
        var cancelledRefund = refundsAfter!.Single(refund => refund.Id == firstRefund.Id);
        Assert.Equal("Cancelled", cancelledRefund.Status);

        var cancelAgain = await client.PostAsJsonAsync(
            $"{ExpensesBase}/{expense.Id}/refunds/{firstRefund.Id}/cancel",
            new { reason = "tekrar", rowVersion = cancelledRefund.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelAgain.StatusCode);

        var blocked = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/cancel", new
        {
            reason = "Yanlış kayıt",
            rowVersion = afterCancelRefund.RowVersion,
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("StateConflict", await ReadCodeAsync(blocked));

        var secondRefund = refundsAfter!.Single(refund => refund.Amount == 60m);
        var cancelSecond = await client.PostAsJsonAsync(
            $"{ExpensesBase}/{expense.Id}/refunds/{secondRefund.Id}/cancel",
            new { reason = "iade iptal", rowVersion = secondRefund.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelSecond.StatusCode);

        var current = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(current);
        var cancelExpense = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/cancel", new
        {
            reason = "Yanlış kayıt",
            rowVersion = current!.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, cancelExpense.StatusCode);
    }

    [Fact]
    public async Task Iptal_edilmis_harcamaya_iade_eklenemez()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);
        var expense = await CreateExpenseAsync(client, category.Id, amount: 100m);

        var cancel = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/cancel", new
        {
            reason = "Yanlış kayıt",
            rowVersion = expense.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var response = await client.PostAsJsonAsync($"{ExpensesBase}/{expense.Id}/refunds", new
        {
            amount = 10m,
            refundedOn = Past,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("StateConflict", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Iade_toplami_altina_tutar_ve_iade_sonrasi_tarih_reddedilir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var category = await CreateCategoryAsync(client);
        var expense = await CreateExpenseAsync(client, category.Id, amount: 100m);
        await AddRefundAsync(client, expense.Id, 60m, Past, null);

        var current = await client.GetFromJsonAsync<ExpenseResponse>($"{ExpensesBase}/{expense.Id}");
        Assert.NotNull(current);

        var belowRefund = await client.PutAsJsonAsync($"{ExpensesBase}/{expense.Id}", new
        {
            amount = 50m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "Cash",
            rowVersion = current!.RowVersion,
        });
        Assert.Equal(HttpStatusCode.BadRequest, belowRefund.StatusCode);
        Assert.Equal("BusinessRuleViolation", await ReadCodeAsync(belowRefund));
        Assert.True(await HasFieldErrorAsync(belowRefund, "amount"));

        var dateAfterRefund = await client.PutAsJsonAsync($"{ExpensesBase}/{expense.Id}", new
        {
            amount = 100m,
            occurredOn = Today,
            categoryId = category.Id,
            paymentMethod = "Cash",
            rowVersion = current.RowVersion,
        });
        Assert.Equal(HttpStatusCode.BadRequest, dateAfterRefund.StatusCode);
        Assert.True(await HasFieldErrorAsync(dateAfterRefund, "occurredOn"));
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_harcama_ve_kategorilerini_goremez()
    {
        using var userA = await RegisterAsync();
        using var userB = await RegisterAsync();

        var category = await CreateCategoryAsync(userA.Client, "Gizli");
        var expense = await CreateExpenseAsync(userA.Client, category.Id, amount: 100m);

        var otherExpense = await userB.Client.GetAsync($"{ExpensesBase}/{expense.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherExpense.StatusCode);

        var otherList = await userB.Client.GetFromJsonAsync<PagedResponse>(ExpensesBase);
        Assert.NotNull(otherList);
        Assert.Equal(0, otherList!.TotalCount);

        var otherCategories = await userB.Client.GetFromJsonAsync<List<CategoryResponse>>(
            $"{CategoriesBase}?includeArchived=true");
        Assert.NotNull(otherCategories);
        Assert.Empty(otherCategories!);

        var otherUpdate = await userB.Client.PutAsJsonAsync($"{ExpensesBase}/{expense.Id}", new
        {
            amount = 1m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "Cash",
            rowVersion = expense.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NotFound, otherUpdate.StatusCode);

        var otherArchive = await userB.Client.PostAsJsonAsync($"{CategoriesBase}/{category.Id}/archive", new
        {
            rowVersion = category.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NotFound, otherArchive.StatusCode);
    }

    [Fact]
    public async Task OpenApi_harcama_ve_kategori_uc_noktalarini_icerir()
    {
        using var session = await RegisterAsync();

        var document = await session.Client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/v1/expenses", document);
        Assert.Contains("/api/v1/expenses/{id}/refunds", document);
        Assert.Contains("/api/v1/expenses/{id}/cancel", document);
        Assert.Contains("/api/v1/categories", document);
        Assert.Contains("/api/v1/categories/{id}/archive", document);
        Assert.DoesNotContain("/api/v1/expenses/_status", document);
    }

    private async Task<Session> RegisterAsync()
    {
        var client = IdempotencyTestClient.Create(fixture.Factory);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"user-{Guid.NewGuid():N}@example.com",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return new Session(client);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(
        HttpClient client,
        string name = "Market")
    {
        var response = await client.PostAsJsonAsync(CategoriesBase, new { name, iconKey = "cart" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        return category!;
    }

    private static async Task<ExpenseResponse> CreateExpenseAsync(
        HttpClient client,
        Guid categoryId,
        decimal amount = 100m,
        DateOnly? occurredOn = null,
        string paymentMethod = "Cash",
        Guid? cardId = null)
    {
        var response = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount,
            currency = "TRY",
            occurredOn = occurredOn ?? Past,
            categoryId,
            description = "test",
            paymentMethod,
            cardId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var expense = await response.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(expense);
        return expense!;
    }

    private static async Task<CardResponse> CreateCardAsync(
        HttpClient client,
        string displayName = "Platinum")
    {
        var response = await client.PostAsJsonAsync(CardsBase, new
        {
            bankName = "Örnek Bank",
            displayName,
            logoKey = "ornek-bank",
            creditLimit = 50_000m,
            statementDay = 25,
            paymentDueDay = 5,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        return card!;
    }

    private static async Task ArchiveCardAsync(HttpClient client, CardResponse card)
    {
        var response = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/archive", new
        {
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<RefundResponse> AddRefundAsync(
        HttpClient client,
        Guid expenseId,
        decimal amount,
        DateOnly refundedOn,
        string? description)
    {
        var response = await client.PostAsJsonAsync($"{ExpensesBase}/{expenseId}/refunds", new
        {
            amount,
            refundedOn,
            description,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var refund = await response.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.NotNull(refund);
        return refund!;
    }

    private static async Task AssertValidationFieldAsync(HttpClient client, string url, string field)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, field));
    }

    private static string? ExpenseBase(HttpResponseMessage response) =>
        response.Headers.Location?.ToString();

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<bool> HasFieldErrorAsync(HttpResponseMessage response, string field)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errors", out var errors) &&
               errors.ValueKind == JsonValueKind.Object &&
               errors.TryGetProperty(field, out _);
    }

    private sealed record Session(HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthResponse(Guid UserId, string AccessToken);

    private sealed record CategoryResponse(
        Guid Id,
        string Name,
        string? IconKey,
        bool IsArchived,
        string RowVersion);

    private sealed record ExpenseResponse(
        Guid Id,
        decimal Amount,
        string Currency,
        DateOnly OccurredOn,
        Guid CategoryId,
        string CategoryName,
        string? Description,
        string PaymentMethod,
        Guid? CardId,
        string Status,
        decimal RefundedAmount,
        decimal NetAmount,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record RefundResponse(
        Guid Id,
        Guid ExpenseId,
        decimal Amount,
        string Currency,
        DateOnly RefundedOn,
        string? Description,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record CardResponse(
        Guid Id,
        string BankName,
        string DisplayName,
        string? LogoKey,
        decimal? CreditLimit,
        int? StatementDay,
        int? PaymentDueDay,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record PagedResponse(
        List<ExpenseResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
