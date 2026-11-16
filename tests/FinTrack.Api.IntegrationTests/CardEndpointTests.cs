using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Kart sözleşmesi (docs/06 §6.1–6.3) ve docs/07 slice 3 kabul testleri.
/// Her test kendi kullanıcısını üretir; testler sıraya bağımlı değildir.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CardEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string CardsBase = "/api/v1/cards";
    private const string ExpensesBase = "/api/v1/expenses";
    private const string CategoriesBase = "/api/v1/categories";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
    private static readonly DateOnly Past = Today.AddDays(-1);

    [Fact]
    public async Task Kart_yasam_dongusu_ve_arsivleme()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var card = await CreateCardAsync(client);
        Assert.Equal("Örnek Bank", card.BankName);
        Assert.Equal("Platinum", card.DisplayName);
        Assert.Equal("ornek-bank", card.LogoKey);
        Assert.Equal(50_000m, card.CreditLimit);
        Assert.Equal(25, card.StatementDay);
        Assert.Equal(5, card.PaymentDueDay);
        Assert.Equal("Active", card.Status);
        Assert.NotEqual(default, card.RecordedAtUtc);
        Assert.Equal(8, Convert.FromBase64String(card.RowVersion).Length);

        var list = await client.GetFromJsonAsync<List<CardResponse>>(CardsBase);
        Assert.NotNull(list);
        Assert.Single(list!);

        var update = await client.PutAsJsonAsync($"{CardsBase}/{card.Id}", new
        {
            bankName = "Yeni Banka",
            displayName = "Gold",
            logoKey = (string?)null,
            creditLimit = (decimal?)null,
            statementDay = 10,
            paymentDueDay = 20,
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Yeni Banka", updated!.BankName);
        Assert.Equal("Gold", updated.DisplayName);
        Assert.Null(updated.LogoKey);
        Assert.Null(updated.CreditLimit);
        Assert.NotEqual(card.RowVersion, updated.RowVersion);

        var stale = await client.PutAsJsonAsync($"{CardsBase}/{card.Id}", new
        {
            bankName = "Eski",
            displayName = "Eski",
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(stale));

        var archive = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/archive", new
        {
            rowVersion = updated.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var defaultList = await client.GetFromJsonAsync<List<CardResponse>>(CardsBase);
        Assert.NotNull(defaultList);
        Assert.Empty(defaultList!);

        var archivedList = await client.GetFromJsonAsync<List<CardResponse>>($"{CardsBase}?status=Archived");
        Assert.NotNull(archivedList);
        var archived = Assert.Single(archivedList!);
        Assert.Equal("Archived", archived.Status);

        var detail = await client.GetFromJsonAsync<CardResponse>($"{CardsBase}/{card.Id}");
        Assert.NotNull(detail);
        Assert.Equal("Archived", detail!.Status);

        // Arşivleme idempotenttir; güncel sürümle tekrar çağrı 204 döner.
        var archiveAgain = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/archive", new
        {
            rowVersion = detail.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, archiveAgain.StatusCode);

        var invalidStatus = await client.GetAsync($"{CardsBase}?status=Bilinmeyen");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        Assert.True(await HasFieldErrorAsync(invalidStatus, "status"));
    }

    [Fact]
    public async Task Kart_isteginde_bilinmeyen_alan_reddedilir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var withCardNumber = await client.PostAsJsonAsync(CardsBase, new
        {
            bankName = "Örnek Bank",
            displayName = "Platinum",
            cardNumber = "4111111111111111",
        });
        Assert.Equal(HttpStatusCode.BadRequest, withCardNumber.StatusCode);

        var withCvv = await client.PostAsJsonAsync(CardsBase, new
        {
            bankName = "Örnek Bank",
            displayName = "Platinum",
            cvv = "123",
        });
        Assert.Equal(HttpStatusCode.BadRequest, withCvv.StatusCode);

        // Geçerli kart sonradan oluşturulabilir; istekler kaydedilmemiştir.
        var list = await client.GetFromJsonAsync<List<CardResponse>>(CardsBase);
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Bakiye_harcama_ve_iadeden_hesaplanir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);
        var category = await CreateCategoryAsync(client);

        var refunded = await CreateExpenseAsync(client, category.Id, card.Id, 100m);
        await CreateExpenseAsync(client, category.Id, card.Id, 200m);
        await CreateExpenseAsync(client, category.Id, cardId: null, amount: 50m);

        await AddRefundAsync(client, refunded.Id, 40m);

        // İade ikinci bir gider oluşturmaz.
        var expenses = await client.GetFromJsonAsync<PagedResponse>(ExpensesBase);
        Assert.NotNull(expenses);
        Assert.Equal(3, expenses!.TotalCount);

        var cardExpenses = await client.GetFromJsonAsync<PagedResponse>($"{ExpensesBase}?cardId={card.Id}");
        Assert.NotNull(cardExpenses);
        Assert.Equal(2, cardExpenses!.TotalCount);

        var balance = await client.GetFromJsonAsync<CardBalanceResponse>($"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(balance);
        Assert.Equal(card.Id, balance!.CardId);
        Assert.Equal("TRY", balance.Currency);
        Assert.Equal(260m, balance.Debt);
        Assert.False(balance.IsInCredit);
        Assert.Equal(300m, balance.Breakdown.Expenses);
        Assert.Equal(40m, balance.Breakdown.Refunds);
        Assert.Equal(0m, balance.Breakdown.CashAdvances);
        Assert.Equal(0m, balance.Breakdown.Payments);
        Assert.Equal(Today, balance.AsOfDate);
    }

    [Fact]
    public async Task Arsivli_kartin_gecmis_bakiyesi_okunur_ve_yeni_harcama_reddedilir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);
        var category = await CreateCategoryAsync(client);

        await CreateExpenseAsync(client, category.Id, card.Id, 150m);

        var archive = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/archive", new
        {
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var balance = await client.GetFromJsonAsync<CardBalanceResponse>($"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(balance);
        Assert.Equal(150m, balance!.Debt);

        var rejected = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount = 10m,
            occurredOn = Past,
            categoryId = category.Id,
            paymentMethod = "CreditCard",
            cardId = card.Id,
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.True(await HasFieldErrorAsync(rejected, "cardId"));
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_kartini_goremez()
    {
        using var userA = await RegisterAsync();
        using var userB = await RegisterAsync();

        var card = await CreateCardAsync(userA.Client);

        var otherDetail = await userB.Client.GetAsync($"{CardsBase}/{card.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherDetail.StatusCode);

        var otherBalance = await userB.Client.GetAsync($"{CardsBase}/{card.Id}/balance");
        Assert.Equal(HttpStatusCode.NotFound, otherBalance.StatusCode);

        var otherList = await userB.Client.GetFromJsonAsync<List<CardResponse>>($"{CardsBase}?status=Archived");
        Assert.NotNull(otherList);
        Assert.Empty(otherList!);

        var otherUpdate = await userB.Client.PutAsJsonAsync($"{CardsBase}/{card.Id}", new
        {
            bankName = "Banka",
            displayName = "Kart",
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NotFound, otherUpdate.StatusCode);

        var otherArchive = await userB.Client.PostAsJsonAsync($"{CardsBase}/{card.Id}/archive", new
        {
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NotFound, otherArchive.StatusCode);
    }

    [Fact]
    public async Task OpenApi_kart_uc_noktalarini_icerir_ve_status_kaldirilmistir()
    {
        using var session = await RegisterAsync();

        var document = await session.Client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/v1/cards", document);
        Assert.Contains("/api/v1/cards/{id}", document);
        Assert.Contains("/api/v1/cards/{id}/archive", document);
        Assert.Contains("/api/v1/cards/{id}/balance", document);
        Assert.DoesNotContain("/api/v1/cards/_status", document);
    }

    private async Task<Session> RegisterAsync()
    {
        var client = fixture.Factory.CreateClient();
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

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client) =>
        await CreateCategoryAsync(client, "Market");

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
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
        Guid? cardId,
        decimal amount)
    {
        var response = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount,
            currency = "TRY",
            occurredOn = Past,
            categoryId,
            description = "test",
            paymentMethod = cardId is null ? "Cash" : "CreditCard",
            cardId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var expense = await response.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(expense);
        return expense!;
    }

    private static async Task<RefundResponse> AddRefundAsync(
        HttpClient client,
        Guid expenseId,
        decimal amount)
    {
        var response = await client.PostAsJsonAsync($"{ExpensesBase}/{expenseId}/refunds", new
        {
            amount,
            refundedOn = Past,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var refund = await response.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.NotNull(refund);
        return refund!;
    }

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

    private sealed record CardBalanceResponse(
        Guid CardId,
        DateOnly AsOfDate,
        string Currency,
        decimal Debt,
        bool IsInCredit,
        BalanceBreakdown Breakdown);

    private sealed record BalanceBreakdown(
        decimal Expenses,
        decimal Refunds,
        decimal CashAdvances,
        decimal Payments);

    private sealed record CategoryResponse(Guid Id, string Name);

    private sealed record ExpenseResponse(Guid Id, decimal Amount, Guid? CardId);

    private sealed record RefundResponse(Guid Id, decimal Amount);

    private sealed record PagedResponse(
        List<ExpenseResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
