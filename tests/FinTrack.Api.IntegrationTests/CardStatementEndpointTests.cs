using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Kart ödemesi ve ekstre sözleşmesi (docs/06 §6.3–6.6) ile docs/07 slice 4
/// kabul testleri. Her test kendi kullanıcısını üretir.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CardStatementEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string CardsBase = "/api/v1/cards";
    private const string ExpensesBase = "/api/v1/expenses";
    private const string CategoriesBase = "/api/v1/categories";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    [Fact]
    public async Task Odeme_ekleme_iptal_ve_bakiye_etkisi()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);
        var category = await CreateCategoryAsync(client);

        await CreateExpenseAsync(
            client,
            category.Id,
            card.Id,
            1000m,
            Today.AddDays(-5));

        var create = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/payments", new
        {
            amount = 250m,
            currency = "TRY",
            paidOn = Today.AddDays(-3),
            note = "Ekstre ödemesi",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var payment = await create.Content.ReadFromJsonAsync<CardPaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(card.Id, payment!.CardId);
        Assert.Equal(250m, payment.Amount);
        Assert.Equal("TRY", payment.Currency);
        Assert.Equal(Today.AddDays(-3), payment.PaidOn);
        Assert.Equal("Ekstre ödemesi", payment.Note);
        Assert.Equal("Active", payment.Status);
        Assert.Equal(8, Convert.FromBase64String(payment.RowVersion).Length);

        // Ödeme gider değildir; gider listesine yeni kayıt eklemez.
        var expenses = await client.GetFromJsonAsync<PagedResponse<ExpenseResponse>>(ExpensesBase);
        Assert.NotNull(expenses);
        Assert.Equal(1, expenses!.TotalCount);

        var payments = await client.GetFromJsonAsync<PagedResponse<CardPaymentResponse>>(
            $"{CardsBase}/{card.Id}/payments");
        Assert.NotNull(payments);
        Assert.Equal(1, payments!.TotalCount);
        Assert.Equal("Active", payments.Items[0].Status);

        var balance = await client.GetFromJsonAsync<CardBalanceResponse>(
            $"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(balance);
        Assert.Equal(750m, balance!.Debt);
        Assert.Equal(1000m, balance.Breakdown.Expenses);
        Assert.Equal(250m, balance.Breakdown.Payments);

        var future = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/payments", new
        {
            amount = 10m,
            currency = "TRY",
            paidOn = Today.AddDays(1),
        });
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.True(await HasFieldErrorAsync(future, "paidOn"));

        var withoutReason = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/payments/{payment.Id}/cancel",
            new { reason = (string?)null, rowVersion = payment.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.True(await HasFieldErrorAsync(withoutReason, "reason"));

        var stale = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/payments/{payment.Id}/cancel",
            new
            {
                reason = "İptal",
                rowVersion = Convert.ToBase64String(new byte[8]),
            });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(stale));

        var cancel = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/payments/{payment.Id}/cancel",
            new { reason = "Yanlış kayıt", rowVersion = payment.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var afterCancel = await client.GetFromJsonAsync<CardBalanceResponse>(
            $"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(afterCancel);
        Assert.Equal(1000m, afterCancel!.Debt);
        Assert.Equal(0m, afterCancel.Breakdown.Payments);

        var activeOnly = await client.GetFromJsonAsync<PagedResponse<CardPaymentResponse>>(
            $"{CardsBase}/{card.Id}/payments");
        Assert.NotNull(activeOnly);
        Assert.Equal(0, activeOnly!.TotalCount);

        var withCancelled = await client.GetFromJsonAsync<PagedResponse<CardPaymentResponse>>(
            $"{CardsBase}/{card.Id}/payments?includeCancelled=true");
        Assert.NotNull(withCancelled);
        Assert.Equal(1, withCancelled!.TotalCount);
        Assert.Equal("Cancelled", withCancelled.Items[0].Status);

        var cancelledOnly = await client.GetFromJsonAsync<PagedResponse<CardPaymentResponse>>(
            $"{CardsBase}/{card.Id}/payments?status=Cancelled");
        Assert.NotNull(cancelledOnly);
        Assert.Equal(1, cancelledOnly!.TotalCount);

        var activeStatus = await client.GetFromJsonAsync<PagedResponse<CardPaymentResponse>>(
            $"{CardsBase}/{card.Id}/payments?status=Active");
        Assert.NotNull(activeStatus);
        Assert.Equal(0, activeStatus!.TotalCount);

        var invalidStatus = await client.GetAsync($"{CardsBase}/{card.Id}/payments?status=Bilinmeyen");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
        Assert.True(await HasFieldErrorAsync(invalidStatus, "status"));
    }

    [Fact]
    public async Task Odeme_arsivli_kartta_ve_diger_kullanicida_reddedilir()
    {
        using var userA = await RegisterAsync();
        using var userB = await RegisterAsync();

        var card = await CreateCardAsync(userA.Client);
        var category = await CreateCategoryAsync(userA.Client);
        await CreateExpenseAsync(userA.Client, category.Id, card.Id, 100m, Today.AddDays(-2));

        var create = await userA.Client.PostAsJsonAsync($"{CardsBase}/{card.Id}/payments", new
        {
            amount = 40m,
            currency = "TRY",
            paidOn = Today.AddDays(-1),
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var payment = await create.Content.ReadFromJsonAsync<CardPaymentResponse>();
        Assert.NotNull(payment);

        var otherCreate = await userB.Client.PostAsJsonAsync($"{CardsBase}/{card.Id}/payments", new
        {
            amount = 10m,
            currency = "TRY",
            paidOn = Today,
        });
        Assert.Equal(HttpStatusCode.NotFound, otherCreate.StatusCode);

        var otherList = await userB.Client.GetAsync($"{CardsBase}/{card.Id}/payments");
        Assert.Equal(HttpStatusCode.NotFound, otherList.StatusCode);

        var otherCancel = await userB.Client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/payments/{payment!.Id}/cancel",
            new { reason = "İptal", rowVersion = payment.RowVersion });
        Assert.Equal(HttpStatusCode.NotFound, otherCancel.StatusCode);

        var archive = await userA.Client.PostAsJsonAsync($"{CardsBase}/{card.Id}/archive", new
        {
            rowVersion = card.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var archivedCreate = await userA.Client.PostAsJsonAsync($"{CardsBase}/{card.Id}/payments", new
        {
            amount = 10m,
            currency = "TRY",
            paidOn = Today,
        });
        Assert.Equal(HttpStatusCode.BadRequest, archivedCreate.StatusCode);
        Assert.True(await HasFieldErrorAsync(archivedCreate, "cardId"));
    }

    [Fact]
    public async Task Ekstre_turetilen_alanlari_okuma_aninda_gunceller()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);
        var category = await CreateCategoryAsync(client);

        // Ekstre yeni borç kaynağı değildir; yalnızca harcama, iade ve ödemeden türetilir.
        var expense = await CreateExpenseAsync(
            client,
            category.Id,
            card.Id,
            1000m,
            Today.AddDays(-10));
        await AddRefundAsync(client, expense.Id, 100m, Today.AddDays(-9));
        await CreatePaymentAsync(client, card.Id, 200m, Today.AddDays(-8));

        var periodStart = Today.AddDays(-20);
        var periodEnd = Today.AddDays(-6);
        var dueDate = Today.AddDays(-1);
        var statement = await CreateStatementAsync(
            client,
            card.Id,
            periodStart,
            periodEnd,
            dueDate,
            900m);
        Assert.Equal("Active", statement.Status);
        Assert.Equal(0m, statement.CarriedOverAmount);
        Assert.Equal(700m, statement.ComputedDebtAtPeriodEnd);
        Assert.Equal(200m, statement.ReconciliationDifference);

        // Dönem içine sonradan girilen ödeme okuma anında düşülür.
        var payment = await CreatePaymentAsync(client, card.Id, 50m, Today.AddDays(-7));
        var withPayment = await client.GetFromJsonAsync<StatementResponse>(
            $"{CardsBase}/{card.Id}/statements/{statement.Id}");
        Assert.NotNull(withPayment);
        Assert.Equal(650m, withPayment!.ComputedDebtAtPeriodEnd);
        Assert.Equal(250m, withPayment.ReconciliationDifference);

        // Dönem sonundan sonraki iade geçmiş ekstreyi değiştirmez.
        await AddRefundAsync(client, expense.Id, 20m, Today.AddDays(-5));

        var unchanged = await client.GetFromJsonAsync<StatementResponse>(
            $"{CardsBase}/{card.Id}/statements/{statement.Id}");
        Assert.NotNull(unchanged);
        Assert.Equal(650m, unchanged!.ComputedDebtAtPeriodEnd);

        // Dönem sonundan sonra (bugün) iptal edilen ödeme kesitte aktif kalır.
        var cancel = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/payments/{payment.Id}/cancel",
            new { reason = "İptal", rowVersion = payment.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var afterCancel = await client.GetFromJsonAsync<StatementResponse>(
            $"{CardsBase}/{card.Id}/statements/{statement.Id}");
        Assert.NotNull(afterCancel);
        Assert.Equal(650m, afterCancel!.ComputedDebtAtPeriodEnd);
    }

    [Fact]
    public async Task Ekstre_donem_cakismasi_iptal_ve_listeleme()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);

        var first = await CreateStatementAsync(
            client,
            card.Id,
            Today.AddDays(-20),
            Today.AddDays(-6),
            Today.AddDays(-1),
            500m);

        var overlap = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/statements", new
        {
            periodStart = Today.AddDays(-10),
            periodEnd = Today.AddDays(-2),
            dueDate = Today,
            statementAmount = 600m,
            currency = "TRY",
        });
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(overlap));
        Assert.True(await HasFieldErrorAsync(overlap, "periodEnd"));

        var adjacent = await CreateStatementAsync(
            client,
            card.Id,
            Today.AddDays(-5),
            Today.AddDays(-1),
            Today,
            700m);
        Assert.Equal("Active", adjacent.Status);

        var list = await client.GetFromJsonAsync<PagedResponse<StatementResponse>>(
            $"{CardsBase}/{card.Id}/statements");
        Assert.NotNull(list);
        Assert.Equal(2, list!.TotalCount);
        Assert.Equal(Today.AddDays(-1), list.Items[0].PeriodEnd);

        var stale = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/statements/{first.Id}/cancel",
            new
            {
                reason = "Yanlış dönem",
                rowVersion = Convert.ToBase64String(new byte[8]),
            });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var withoutReason = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/statements/{first.Id}/cancel",
            new { reason = (string?)null, rowVersion = first.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.True(await HasFieldErrorAsync(withoutReason, "reason"));

        var cancel = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/statements/{first.Id}/cancel",
            new { reason = "Yanlış dönem", rowVersion = first.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var cancelled = await client.GetFromJsonAsync<StatementResponse>(
            $"{CardsBase}/{card.Id}/statements/{first.Id}");
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);

        var defaultList = await client.GetFromJsonAsync<PagedResponse<StatementResponse>>(
            $"{CardsBase}/{card.Id}/statements");
        Assert.NotNull(defaultList);
        Assert.Equal(1, defaultList!.TotalCount);

        var withCancelled = await client.GetFromJsonAsync<PagedResponse<StatementResponse>>(
            $"{CardsBase}/{card.Id}/statements?includeCancelled=true");
        Assert.NotNull(withCancelled);
        Assert.Equal(2, withCancelled!.TotalCount);

        var cancelledOnly = await client.GetFromJsonAsync<PagedResponse<StatementResponse>>(
            $"{CardsBase}/{card.Id}/statements?status=Cancelled");
        Assert.NotNull(cancelledOnly);
        Assert.Equal(1, cancelledOnly!.TotalCount);

        // İptal edilen ekstre dönemi serbest bırakır.
        var samePeriod = await CreateStatementAsync(
            client,
            card.Id,
            Today.AddDays(-20),
            Today.AddDays(-6),
            Today.AddDays(-1),
            500m);
        Assert.Equal("Active", samePeriod.Status);

        var unknownField = await client.PostAsJsonAsync($"{CardsBase}/{card.Id}/statements", new
        {
            periodStart = Today.AddDays(-4),
            periodEnd = Today.AddDays(-3),
            dueDate = Today,
            statementAmount = 100m,
            carriedOverAmount = 10m,
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
    }

    [Fact]
    public async Task Bakiye_asOf_kesiti_ve_dogrulama()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);
        var category = await CreateCategoryAsync(client);

        await CreateExpenseAsync(client, category.Id, card.Id, 100m, Today.AddDays(-10));
        await CreateExpenseAsync(client, category.Id, card.Id, 200m, Today.AddDays(-5));
        var refunded = await CreateExpenseAsync(
            client,
            category.Id,
            card.Id,
            30m,
            Today.AddDays(-10));
        await AddRefundAsync(client, refunded.Id, 30m, Today.AddDays(-3));
        await CreatePaymentAsync(client, card.Id, 25m, Today.AddDays(-2));

        var current = await client.GetFromJsonAsync<CardBalanceResponse>(
            $"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(current);
        Assert.Equal(Today, current!.AsOfDate);
        Assert.Equal(275m, current.Debt);

        var beforeRefund = await client.GetFromJsonAsync<CardBalanceResponse>(
            $"{CardsBase}/{card.Id}/balance?asOf={Today.AddDays(-6):yyyy-MM-dd}");
        Assert.NotNull(beforeRefund);
        Assert.Equal(Today.AddDays(-6), beforeRefund!.AsOfDate);
        Assert.Equal(130m, beforeRefund.Debt);
        Assert.Equal(0m, beforeRefund.Breakdown.Refunds);
        Assert.Equal(0m, beforeRefund.Breakdown.Payments);

        var future = await client.GetAsync(
            $"{CardsBase}/{card.Id}/balance?asOf={Today.AddDays(1):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(future));
        Assert.True(await HasFieldErrorAsync(future, "asOf"));

        var malformed = await client.GetAsync($"{CardsBase}/{card.Id}/balance?asOf=2026-13-40");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.True(await HasFieldErrorAsync(malformed, "asOf"));
    }

    [Fact]
    public async Task OpenApi_odeme_ve_ekstre_uc_noktalarini_icerir()
    {
        using var session = await RegisterAsync();

        var document = await session.Client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/v1/cards/{id}/payments", document);
        Assert.Contains("/api/v1/cards/{id}/payments/{paymentId}/cancel", document);
        Assert.Contains("/api/v1/cards/{id}/statements", document);
        Assert.Contains("/api/v1/cards/{id}/statements/{statementId}", document);
        Assert.Contains("/api/v1/cards/{id}/statements/{statementId}/cancel", document);
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

    private static async Task<CardResponse> CreateCardAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(CardsBase, new
        {
            bankName = "Örnek Bank",
            displayName = "Platinum",
            statementDay = 25,
            paymentDueDay = 5,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        return card!;
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(CategoriesBase, new
        {
            name = "Market",
            iconKey = "cart",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        return category!;
    }

    private static async Task<ExpenseResponse> CreateExpenseAsync(
        HttpClient client,
        Guid categoryId,
        Guid cardId,
        decimal amount,
        DateOnly occurredOn)
    {
        var response = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount,
            currency = "TRY",
            occurredOn,
            categoryId,
            description = "test",
            paymentMethod = "CreditCard",
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
        decimal amount,
        DateOnly refundedOn)
    {
        var response = await client.PostAsJsonAsync($"{ExpensesBase}/{expenseId}/refunds", new
        {
            amount,
            refundedOn,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var refund = await response.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.NotNull(refund);
        return refund!;
    }

    private static async Task<CardPaymentResponse> CreatePaymentAsync(
        HttpClient client,
        Guid cardId,
        decimal amount,
        DateOnly paidOn)
    {
        var response = await client.PostAsJsonAsync($"{CardsBase}/{cardId}/payments", new
        {
            amount,
            currency = "TRY",
            paidOn,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<CardPaymentResponse>();
        Assert.NotNull(payment);
        return payment!;
    }

    private static async Task<StatementResponse> CreateStatementAsync(
        HttpClient client,
        Guid cardId,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        decimal statementAmount)
    {
        var response = await client.PostAsJsonAsync($"{CardsBase}/{cardId}/statements", new
        {
            periodStart,
            periodEnd,
            dueDate,
            statementAmount,
            currency = "TRY",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var statement = await response.Content.ReadFromJsonAsync<StatementResponse>();
        Assert.NotNull(statement);
        return statement!;
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

    private sealed record CardPaymentResponse(
        Guid Id,
        Guid CardId,
        decimal Amount,
        string Currency,
        DateOnly PaidOn,
        string? Note,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record StatementResponse(
        Guid Id,
        Guid CardId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        DateOnly DueDate,
        decimal StatementAmount,
        string Currency,
        decimal CarriedOverAmount,
        decimal ComputedDebtAtPeriodEnd,
        decimal ReconciliationDifference,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record CategoryResponse(Guid Id, string Name);

    private sealed record ExpenseResponse(Guid Id, decimal Amount, Guid? CardId);

    private sealed record RefundResponse(Guid Id, decimal Amount);

    private sealed record PagedResponse<T>(
        List<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
