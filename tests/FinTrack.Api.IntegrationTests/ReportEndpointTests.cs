using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Raporlama uç noktaları (docs/06 §8) ve Slice 8 kabul testleri (docs/07).
/// Her test kendi kullanıcısını oluşturur; veriler API üzerinden yazılır,
/// raporlar yalnızca okunur.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string ReportsBase = "/api/v1/reports";
    private const string CategoriesBase = "/api/v1/categories";
    private const string ExpensesBase = "/api/v1/expenses";
    private const string InflowsBase = "/api/v1/cash-inflows";
    private const string CardsBase = "/api/v1/cards";
    private const string LoansBase = "/api/v1/loans";
    private const string DebtsBase = "/api/v1/personal-debts";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    [Fact]
    public async Task Aylik_ozet_dokuman_02_ornegini_birebir_uretir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        await CreateInflowAsync(client, 80_000m, "Salary");
        await CreateInflowAsync(client, 1_000m, "Unknown");

        var card = await CreateCardAsync(client);
        await CreateAdvanceAsync(client, card.Id, 5_000m);

        await CreateLoanAsync(client, 30_000m, 3_000m, 10, Today.AddDays(7));
        var category = await CreateCategoryAsync(client);
        var expense = await CreateExpenseAsync(client, category.Id, 50_000m);
        await AddRefundAsync(client, expense.Id, 2_000m);

        var report = await client.GetFromJsonAsync<MonthlyReportResponse>(
            $"{ReportsBase}/monthly?year={Today.Year}&month={Today.Month}");

        Assert.NotNull(report);
        Assert.Equal(Today.Year, report!.Period.Year);
        Assert.Equal(Today.Month, report.Period.Month);
        Assert.Equal(new DateOnly(Today.Year, Today.Month, 1), report.Period.From);
        Assert.Equal(
            new DateOnly(Today.Year, Today.Month, 1).AddMonths(1).AddDays(-1),
            report.Period.To);
        Assert.Equal("Europe/Istanbul", report.Period.TimeZone);
        Assert.Equal("TRY", report.Currency);
        Assert.Equal(80_000m, report.EarnedIncome);

        Assert.Equal(35_000m, report.OtherInflows.Total);
        Assert.Equal(30_000m, report.OtherInflows.Items.Single(x => x.Source == "BankLoan").Amount);
        Assert.Equal(5_000m, report.OtherInflows.Items.Single(x => x.Source == "CashAdvance").Amount);
        Assert.Equal(1_000m, report.UnknownInflows.Total);
        Assert.Equal(1, report.UnknownInflows.Count);

        Assert.Equal(50_000m, report.Expense.Gross);
        Assert.Equal(2_000m, report.Expense.Refunds);
        Assert.Equal(48_000m, report.Expense.Net);
        Assert.Equal(32_000m, report.IncomeExpenseDifference);
        Assert.False(report.IsBankBalance);
        Assert.Equal("Gelir−gider farkı banka bakiyesi değildir.", report.Disclaimer);
        Assert.Equal(48_000m, Assert.Single(report.Categories).NetAmount);
    }

    [Fact]
    public async Task Borc_ozeti_nakit_avansi_bir_kez_sayar_negatif_karti_dusmez()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var card = await CreateCardAsync(client);
        await CreateAdvanceAsync(client, card.Id, 4_200m);
        await CreateLoanAsync(client, 30_000m, 3_000m, 10, Today.AddDays(7));
        await CreatePersonalDebtAsync(client, 4_000m);

        // Bu kart alacağıdır; başka borçlardan mahsup edilmez.
        var creditCard = await CreateCardAsync(client, "Fazla Ödemeli");
        await CreatePaymentAsync(client, creditCard.Id, 800m);

        var report = await client.GetFromJsonAsync<DebtSummaryResponse>(
            $"{ReportsBase}/debt-summary");

        Assert.NotNull(report);
        Assert.Equal("TRY", report!.Currency);
        Assert.Equal(38_200m, report.TotalDebt);
        Assert.Equal(800m, report.CardCreditTotal);

        var advanceCard = Assert.Single(report.Cards, x => x.CardId == card.Id);
        Assert.Equal(4_200m, advanceCard.Debt);
        Assert.False(advanceCard.IsInCredit);

        var inCredit = Assert.Single(report.Cards, x => x.CardId == creditCard.Id);
        Assert.Equal(-800m, inCredit.Debt);
        Assert.True(inCredit.IsInCredit);

        Assert.Equal(30_000m, Assert.Single(report.Loans).RemainingAmount);
        Assert.Equal(4_000m, Assert.Single(report.PersonalDebts).RemainingAmount);
        Assert.Equal(DateTimeKind.Utc, report.AsOfUtc.Kind);
    }

    [Fact]
    public async Task Yaklasan_odemeler_taksit_ve_ekstreleri_listeler_yeni_kayit_olusturmaz()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var card = await CreateCardAsync(client);
        await CreateStatementAsync(
            client,
            card.Id,
            Today.AddDays(-20),
            Today.AddDays(-6),
            Today.AddDays(7),
            6_500m);

        var loan = await CreateLoanAsync(client, 3_000m, 3_000m, 3, Today.AddDays(7));

        var loansBefore = await client.GetFromJsonAsync<PagedResponse<JsonElement>>(LoansBase);
        var statementsBefore = await client.GetFromJsonAsync<PagedResponse<JsonElement>>(
            $"{CardsBase}/{card.Id}/statements");
        Assert.NotNull(loansBefore);
        Assert.NotNull(statementsBefore);

        var report = await client.GetFromJsonAsync<UpcomingPaymentsResponse>(
            $"{ReportsBase}/upcoming-payments?days=30");

        Assert.NotNull(report);
        Assert.Equal(Today, report!.From);
        Assert.Equal(Today.AddDays(30), report.To);
        Assert.Equal("TRY", report.Currency);

        var installment = Assert.Single(report.Items, x => x.Type == "LoanInstallment");
        Assert.Equal(loan.Id, installment.LoanId);
        Assert.Equal(1, installment.InstallmentNumber);
        Assert.Equal(Today.AddDays(7), installment.DueDate);
        Assert.Equal(3_000m, installment.Amount);

        var statement = Assert.Single(report.Items, x => x.Type == "CardStatement");
        Assert.Equal(card.Id, statement.CardId);
        Assert.Equal(Today.AddDays(7), statement.DueDate);
        Assert.Equal(6_500m, statement.Amount);
        Assert.NotNull(statement.StatementId);

        var loansAfter = await client.GetFromJsonAsync<PagedResponse<JsonElement>>(LoansBase);
        var statementsAfter = await client.GetFromJsonAsync<PagedResponse<JsonElement>>(
            $"{CardsBase}/{card.Id}/statements");
        Assert.Equal(loansBefore!.TotalCount, loansAfter!.TotalCount);
        Assert.Equal(statementsBefore!.TotalCount, statementsAfter!.TotalCount);
    }

    [Theory]
    [InlineData($"{ReportsBase}/monthly?year=1999&month=13", "year")]
    [InlineData($"{ReportsBase}/monthly?year=2026&month=0", "month")]
    [InlineData($"{ReportsBase}/upcoming-payments?days=0", "days")]
    [InlineData($"{ReportsBase}/upcoming-payments?days=91", "days")]
    public async Task Gecersiz_parametreler_alan_hata_doner(string url, string field)
    {
        using var session = await RegisterAsync();

        var response = await session.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, field));
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
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return new Session(client);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(CategoriesBase, new
        {
            name = $"Market-{Guid.NewGuid():N}",
            iconKey = "cart",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        return category!;
    }

    private static async Task CreateInflowAsync(HttpClient client, decimal amount, string source)
    {
        var response = await client.PostAsJsonAsync(InflowsBase, new
        {
            amount,
            currency = "TRY",
            occurredOn = Today,
            source,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<CardResponse> CreateCardAsync(
        HttpClient client,
        string displayName = "Platinum")
    {
        var response = await client.PostAsJsonAsync(CardsBase, new
        {
            bankName = "Örnek Bank",
            displayName,
            statementDay = 25,
            paymentDueDay = 5,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        return card!;
    }

    private static async Task CreateAdvanceAsync(HttpClient client, Guid cardId, decimal amount)
    {
        var response = await client.PostAsJsonAsync(
            $"{CardsBase}/{cardId}/cash-advances",
            new { amount, currency = "TRY", takenOn = Today });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<ExpenseResponse> CreateExpenseAsync(
        HttpClient client,
        Guid categoryId,
        decimal amount)
    {
        var response = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount,
            currency = "TRY",
            occurredOn = Today,
            categoryId,
            description = "Slice 8",
            paymentMethod = "Cash",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var expense = await response.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(expense);
        return expense!;
    }

    private static async Task AddRefundAsync(HttpClient client, Guid expenseId, decimal amount)
    {
        var response = await client.PostAsJsonAsync(
            $"{ExpensesBase}/{expenseId}/refunds",
            new { amount, refundedOn = Today });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<LoanResponse> CreateLoanAsync(
        HttpClient client,
        decimal principal,
        decimal monthlyPayment,
        int installmentCount,
        DateOnly firstDueDate)
    {
        var response = await client.PostAsJsonAsync(LoansBase, new
        {
            principal,
            monthlyPayment,
            installmentCount,
            firstDueDate,
            bankName = "Örnek Bank",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(loan);
        return loan!;
    }

    private static async Task CreatePersonalDebtAsync(HttpClient client, decimal principal)
    {
        var response = await client.PostAsJsonAsync(DebtsBase, new
        {
            counterpartyName = "Ahmet",
            principal,
            borrowedOn = Today,
            dueDate = Today.AddDays(30),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task CreatePaymentAsync(HttpClient client, Guid cardId, decimal amount)
    {
        var response = await client.PostAsJsonAsync(
            $"{CardsBase}/{cardId}/payments",
            new { amount, currency = "TRY", paidOn = Today });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task CreateStatementAsync(
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
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }

    private static async Task<bool> HasFieldErrorAsync(HttpResponseMessage response, string field)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Object
            && errors.TryGetProperty(field, out _);
    }

    private sealed record Session(HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthResponse(Guid UserId, string AccessToken);

    private sealed record CategoryResponse(Guid Id, string Name);
    private sealed record CardResponse(Guid Id, string DisplayName);
    private sealed record ExpenseResponse(Guid Id, decimal Amount);

    private sealed record LoanResponse(Guid Id, decimal RemainingAmount);

    private sealed record PagedResponse<T>(
        List<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record PeriodResponse(
        int Year,
        int Month,
        DateOnly From,
        DateOnly To,
        string TimeZone);

    private sealed record OtherInflowsResponse(
        decimal Total,
        List<OtherInflowItemResponse> Items);

    private sealed record OtherInflowItemResponse(string Source, decimal Amount);

    private sealed record UnknownInflowsResponse(decimal Total, int Count);

    private sealed record ExpenseTotalsResponse(decimal Gross, decimal Refunds, decimal Net);

    private sealed record CategoryBreakdownResponse(
        Guid CategoryId,
        string CategoryName,
        decimal NetAmount);

    private sealed record MonthlyReportResponse(
        PeriodResponse Period,
        string Currency,
        decimal EarnedIncome,
        OtherInflowsResponse OtherInflows,
        UnknownInflowsResponse UnknownInflows,
        ExpenseTotalsResponse Expense,
        decimal IncomeExpenseDifference,
        bool IsBankBalance,
        string Disclaimer,
        List<CategoryBreakdownResponse> Categories);

    private sealed record CardDebtResponse(
        Guid CardId,
        string DisplayName,
        decimal Debt,
        bool IsInCredit);

    private sealed record LoanDebtResponse(
        Guid LoanId,
        string? BankName,
        decimal RemainingAmount);

    private sealed record PersonalDebtResponse(
        Guid PersonalDebtId,
        string CounterpartyName,
        decimal RemainingAmount);

    private sealed record DebtSummaryResponse(
        DateTime AsOfUtc,
        string Currency,
        decimal TotalDebt,
        decimal CardCreditTotal,
        List<CardDebtResponse> Cards,
        List<LoanDebtResponse> Loans,
        List<PersonalDebtResponse> PersonalDebts);

    private sealed record UpcomingPaymentResponse(
        string Type,
        Guid? LoanId,
        int? InstallmentNumber,
        Guid? CardId,
        Guid? StatementId,
        DateOnly DueDate,
        decimal Amount);

    private sealed record UpcomingPaymentsResponse(
        DateOnly From,
        DateOnly To,
        string Currency,
        List<UpcomingPaymentResponse> Items);
}
