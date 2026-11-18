using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Slice 7 kredi ve kişisel borç API sözleşmesi (docs/06 §7, docs/07 Slice 7).
/// Her test kendi kullanıcısını oluşturur; para girişi bağlantıları ADR-0009
/// kapsamında aynı işlemde doğrulanır.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DebtEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string LoansBase = "/api/v1/loans";
    private const string DebtsBase = "/api/v1/personal-debts";
    private const string InflowsBase = "/api/v1/cash-inflows";
    private const string StaleRowVersion = "AAAAAAAAAAA=";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    [Fact]
    public async Task Kredi_tam_vade_plani_ve_bagli_para_girisiyle_olusturulur()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var description = $"Slice7 kredi {Guid.NewGuid():N}";
        var firstDueDate = Today.AddDays(7);

        var response = await client.PostAsJsonAsync(LoansBase, new
        {
            principal = 30_000m,
            monthlyPayment = 3_000m,
            installmentCount = 12,
            firstDueDate,
            bankName = "  Örnek Bank  ",
            description,
            currency = "try",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(loan);
        Assert.Equal($"{LoansBase}/{loan!.Id}", response.Headers.Location?.ToString());
        Assert.Equal(30_000m, loan.Principal);
        Assert.Equal(3_000m, loan.MonthlyPayment);
        Assert.Equal(12, loan.InstallmentCount);
        Assert.Equal(36_000m, loan.TotalRepayment);
        Assert.Equal(36_000m, loan.RemainingAmount);
        Assert.Equal(firstDueDate, loan.FirstDueDate);
        Assert.Equal("Örnek Bank", loan.BankName);
        Assert.Equal(description, loan.Description);
        Assert.Equal("TRY", loan.Currency);
        Assert.Equal("Active", loan.Status);
        Assert.Equal(8, Convert.FromBase64String(loan.RowVersion).Length);

        Assert.Equal(12, loan.Installments.Count);
        for (var number = 1; number <= 12; number++)
        {
            var installment = loan.Installments.Single(i => i.Number == number);
            Assert.Equal(3_000m, installment.Amount);
            Assert.Equal(AddMonthsWithDayAnchor(firstDueDate, number - 1), installment.DueDate);
            Assert.Equal("Planned", installment.Status);
            Assert.Null(installment.Payment);
        }

        var inflow = await FindInflowAsync(client, "BankLoan", loan.Id);
        Assert.NotNull(inflow);
        Assert.Equal(30_000m, inflow!.Amount);
        Assert.Equal(Today, inflow.OccurredOn);
        Assert.Equal(description, inflow.Description);
        Assert.Equal("Loan", inflow.DebtLink?.Type);

        var fetched = await client.GetFromJsonAsync<LoanResponse>($"{LoansBase}/{loan.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(loan.Id, fetched!.Id);

        var list = await client.GetFromJsonAsync<PagedResponse<LoanResponse>>(LoansBase);
        Assert.NotNull(list);
        Assert.Contains(list!.Items, item => item.Id == loan.Id);
    }

    [Fact]
    public async Task Taksit_odeme_ve_iptali_kalan_tutari_gunceller()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var loan = await CreateLoanAsync(client, 3_000m, 1_000m, 3);

        var outOfOrder = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/2/pay",
            new { paidOn = Today, rowVersion = loan.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, outOfOrder.StatusCode);
        Assert.Equal("StateConflict", await ReadCodeAsync(outOfOrder));

        var stale = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/pay",
            new { paidOn = Today, rowVersion = StaleRowVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(stale));

        var paid = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/pay",
            new { paidOn = Today, rowVersion = loan.RowVersion });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var afterPayment = await paid.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(afterPayment);
        Assert.Equal(2_000m, afterPayment!.RemainingAmount);
        var first = afterPayment.Installments.Single(i => i.Number == 1);
        Assert.Equal("Paid", first.Status);
        Assert.Equal(Today, first.PaidOn);
        Assert.NotNull(first.Payment);
        Assert.Equal(1_000m, first.Payment!.Amount);
        Assert.Equal(8, Convert.FromBase64String(first.Payment.RowVersion).Length);

        var wrongPaymentVersion = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/payment/cancel",
            new { reason = "Yanlış", rowVersion = StaleRowVersion });
        Assert.Equal(HttpStatusCode.Conflict, wrongPaymentVersion.StatusCode);

        var cancelled = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/payment/cancel",
            new { reason = "Yanlış ödeme", rowVersion = first.Payment.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        var reloaded = await client.GetFromJsonAsync<LoanResponse>($"{LoansBase}/{loan.Id}");
        Assert.NotNull(reloaded);
        Assert.Equal(3_000m, reloaded!.RemainingAmount);
        var planned = reloaded.Installments.Single(i => i.Number == 1);
        Assert.Equal("Planned", planned.Status);
        Assert.Null(planned.Payment);

        var repaid = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/pay",
            new { paidOn = Today, rowVersion = reloaded.RowVersion });
        Assert.Equal(HttpStatusCode.OK, repaid.StatusCode);

        var cancelWithPayment = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/cancel",
            new { reason = "Vazgeçtim", rowVersion = loan.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, cancelWithPayment.StatusCode);
        Assert.Equal("StateConflict", await ReadCodeAsync(cancelWithPayment));
    }

    [Fact]
    public async Task Kredi_iptali_bagli_para_girisini_de_iptal_eder()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var loan = await CreateLoanAsync(client, 2_000m, 1_000m, 2);

        var cancelled = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/cancel",
            new { reason = "Vazgeçtim", rowVersion = loan.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        var active = await client.GetFromJsonAsync<PagedResponse<LoanResponse>>(LoansBase);
        Assert.NotNull(active);
        Assert.DoesNotContain(active!.Items, item => item.Id == loan.Id);

        var withCancelled = await client.GetFromJsonAsync<PagedResponse<LoanResponse>>(
            $"{LoansBase}?includeCancelled=true");
        Assert.NotNull(withCancelled);
        Assert.Equal("Cancelled", withCancelled!.Items.Single(item => item.Id == loan.Id).Status);

        var inflow = await FindInflowAsync(client, "BankLoan", loan.Id);
        Assert.NotNull(inflow);
        Assert.Equal("Cancelled", inflow!.Status);

        var payCancelled = await client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/pay",
            new { paidOn = Today, rowVersion = loan.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, payCancelled.StatusCode);
    }

    [Fact]
    public async Task Kisisel_borc_ve_geriOdeme_ve_iptali_kalan_tutari_gunceller()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var borrowedOn = Today.AddDays(-10);
        var description = $"Slice7 borç {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(DebtsBase, new
        {
            counterpartyName = "  Ali  ",
            principal = 5_000m,
            borrowedOn,
            dueDate = Today.AddDays(20),
            description,
            currency = "TRY",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var debt = await response.Content.ReadFromJsonAsync<PersonalDebtResponse>();
        Assert.NotNull(debt);
        Assert.Equal($"{DebtsBase}/{debt!.Id}", response.Headers.Location?.ToString());
        Assert.Equal("Ali", debt.CounterpartyName);
        Assert.Equal(5_000m, debt.RemainingAmount);
        Assert.Equal(borrowedOn, debt.BorrowedOn);
        Assert.Equal("Active", debt.Status);
        Assert.Empty(debt.Repayments);
        Assert.Equal(8, Convert.FromBase64String(debt.RowVersion).Length);

        var inflow = await FindInflowAsync(client, "PersonalBorrowing", debt.Id);
        Assert.NotNull(inflow);
        Assert.Equal(5_000m, inflow!.Amount);
        Assert.Equal(borrowedOn, inflow.OccurredOn);
        Assert.Equal("PersonalDebt", inflow.DebtLink?.Type);

        var added = await client.PostAsJsonAsync(
            $"{DebtsBase}/{debt.Id}/repayments",
            new { amount = 1_500m, paidOn = Today.AddDays(-5), note = "İlk ödeme" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var repayment = await added.Content.ReadFromJsonAsync<RepaymentResponse>();
        Assert.NotNull(repayment);
        Assert.Equal(debt.Id, repayment!.PersonalDebtId);
        Assert.Equal(1_500m, repayment.Amount);
        Assert.Equal("Active", repayment.Status);
        Assert.Equal(8, Convert.FromBase64String(repayment.RowVersion).Length);

        var withRepayment = await client.GetFromJsonAsync<PersonalDebtResponse>($"{DebtsBase}/{debt.Id}");
        Assert.NotNull(withRepayment);
        Assert.Equal(3_500m, withRepayment!.RemainingAmount);
        Assert.Equal(repayment.Id, Assert.Single(withRepayment.Repayments).Id);

        var repayments = await client.GetFromJsonAsync<List<RepaymentResponse>>(
            $"{DebtsBase}/{debt.Id}/repayments");
        Assert.NotNull(repayments);
        Assert.Equal(repayment.Id, Assert.Single(repayments!).Id);

        var cancelledRepayment = await client.PostAsJsonAsync(
            $"{DebtsBase}/{debt.Id}/repayments/{repayment.Id}/cancel",
            new { reason = "Yanlış", rowVersion = repayment.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelledRepayment.StatusCode);

        var restored = await client.GetFromJsonAsync<PersonalDebtResponse>($"{DebtsBase}/{debt.Id}");
        Assert.NotNull(restored);
        Assert.Equal(5_000m, restored!.RemainingAmount);
        Assert.Empty(restored.Repayments);

        var cancelledDebt = await client.PostAsJsonAsync(
            $"{DebtsBase}/{debt.Id}/cancel",
            new { reason = "Kapatıldı", rowVersion = restored.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelledDebt.StatusCode);

        var cancelledInflow = await FindInflowAsync(client, "PersonalBorrowing", debt.Id);
        Assert.NotNull(cancelledInflow);
        Assert.Equal("Cancelled", cancelledInflow!.Status);
    }

    [Fact]
    public async Task Aktif_geriOdeme_varken_borc_iptal_edilemez()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var debt = await CreateDebtAsync(client, 1_000m, Today.AddDays(-1));

        var repayment = await client.PostAsJsonAsync(
            $"{DebtsBase}/{debt.Id}/repayments",
            new { amount = 400m, paidOn = Today, note = (string?)null });
        Assert.Equal(HttpStatusCode.Created, repayment.StatusCode);

        var current = await client.GetFromJsonAsync<PersonalDebtResponse>($"{DebtsBase}/{debt.Id}");
        Assert.NotNull(current);

        var cancel = await client.PostAsJsonAsync(
            $"{DebtsBase}/{debt.Id}/cancel",
            new { reason = "Vazgeçtim", rowVersion = current!.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("StateConflict", await ReadCodeAsync(cancel));

        var inflow = await FindInflowAsync(client, "PersonalBorrowing", debt.Id);
        Assert.NotNull(inflow);
        Assert.Equal("Active", inflow!.Status);
    }

    [Fact]
    public async Task Baska_kullanicinin_kredi_ve_borcu_gorunmez()
    {
        using var owner = await RegisterAsync();
        using var other = await RegisterAsync();
        var loan = await CreateLoanAsync(owner.Client, 1_000m, 500m, 2);
        var debt = await CreateDebtAsync(owner.Client, 1_000m, Today.AddDays(-1));

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"{LoansBase}/{loan.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"{DebtsBase}/{debt.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/installments/1/pay",
            new { paidOn = Today, rowVersion = loan.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync(
            $"{DebtsBase}/{debt.Id}/repayments",
            new { amount = 100m, paidOn = Today, note = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsJsonAsync(
            $"{LoansBase}/{loan.Id}/cancel",
            new { reason = "İptal", rowVersion = loan.RowVersion })).StatusCode);

        var inflows = await other.Client.GetFromJsonAsync<PagedResponse<CashInflowResponse>>(InflowsBase);
        Assert.NotNull(inflows);
        Assert.DoesNotContain(inflows!.Items, item => item.DebtLink?.Id == loan.Id);
    }

    [Fact]
    public async Task Turetilmis_alanlar_istemciden_red_edilir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var loanTotal = await client.PostAsJsonAsync(LoansBase, new
        {
            principal = 1_000m,
            monthlyPayment = 1_000m,
            installmentCount = 1,
            firstDueDate = Today.AddDays(1),
            totalRepayment = 1_000m,
        });
        Assert.Equal(HttpStatusCode.BadRequest, loanTotal.StatusCode);

        var loanRemaining = await client.PostAsJsonAsync(LoansBase, new
        {
            principal = 1_000m,
            monthlyPayment = 1_000m,
            installmentCount = 1,
            firstDueDate = Today.AddDays(1),
            remainingAmount = 1_000m,
        });
        Assert.Equal(HttpStatusCode.BadRequest, loanRemaining.StatusCode);

        var debtRemaining = await client.PostAsJsonAsync(DebtsBase, new
        {
            counterpartyName = "Ali",
            principal = 1_000m,
            borrowedOn = Today.AddDays(-1),
            remainingAmount = 1_000m,
        });
        Assert.Equal(HttpStatusCode.BadRequest, debtRemaining.StatusCode);

        var loans = await client.GetFromJsonAsync<PagedResponse<LoanResponse>>(LoansBase);
        Assert.NotNull(loans);
        Assert.Empty(loans!.Items);
        var debts = await client.GetFromJsonAsync<PagedResponse<PersonalDebtResponse>>(DebtsBase);
        Assert.NotNull(debts);
        Assert.Empty(debts!.Items);
    }

    [Fact]
    public async Task Otuz_bir_gunu_olan_aylarin_taksitleri_ay_sonuna_sabitlenir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var firstDueDate = new DateOnly(Today.Year, 1, 31);
        if (firstDueDate <= Today)
        {
            firstDueDate = new DateOnly(Today.Year + 1, 1, 31);
        }

        var loan = await CreateLoanAsync(client, 3_000m, 1_000m, 3, firstDueDate);

        Assert.Equal(3, loan.Installments.Count);
        Assert.Equal(firstDueDate, loan.Installments.Single(i => i.Number == 1).DueDate);
        Assert.Equal(AddMonthsWithDayAnchor(firstDueDate, 1), loan.Installments.Single(i => i.Number == 2).DueDate);
        Assert.Equal(AddMonthsWithDayAnchor(firstDueDate, 2), loan.Installments.Single(i => i.Number == 3).DueDate);
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

    private static async Task<LoanResponse> CreateLoanAsync(
        HttpClient client,
        decimal principal,
        decimal monthlyPayment,
        int count,
        DateOnly? firstDueDate = null)
    {
        var response = await client.PostAsJsonAsync(LoansBase, new
        {
            principal,
            monthlyPayment,
            installmentCount = count,
            firstDueDate = firstDueDate ?? Today.AddDays(7),
            bankName = (string?)null,
            description = (string?)null,
            currency = "TRY",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(loan);
        return loan!;
    }

    private static async Task<PersonalDebtResponse> CreateDebtAsync(
        HttpClient client,
        decimal principal,
        DateOnly borrowedOn)
    {
        var response = await client.PostAsJsonAsync(DebtsBase, new
        {
            counterpartyName = "Ali",
            principal,
            borrowedOn,
            dueDate = Today.AddDays(30),
            description = (string?)null,
            currency = "TRY",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var debt = await response.Content.ReadFromJsonAsync<PersonalDebtResponse>();
        Assert.NotNull(debt);
        return debt!;
    }

    private static async Task<CashInflowResponse?> FindInflowAsync(
        HttpClient client,
        string source,
        Guid debtId)
    {
        var inflows = await client.GetFromJsonAsync<PagedResponse<CashInflowResponse>>(
            $"{InflowsBase}?source={source}&includeCancelled=true&pageSize=200");
        Assert.NotNull(inflows);
        return inflows!.Items.SingleOrDefault(item => item.DebtLink?.Id == debtId);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static DateOnly AddMonthsWithDayAnchor(DateOnly start, int months)
    {
        var absoluteMonth = start.Month + months;
        var year = start.Year + ((absoluteMonth - 1) / 12);
        var month = ((absoluteMonth - 1) % 12) + 1;
        var day = Math.Min(start.Day, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }

    private sealed record Session(HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthResponse(Guid UserId, string AccessToken);

    private sealed record LoanResponse(
        Guid Id,
        decimal Principal,
        decimal MonthlyPayment,
        int InstallmentCount,
        decimal TotalRepayment,
        DateOnly FirstDueDate,
        string? BankName,
        string? Description,
        string Currency,
        string Status,
        decimal RemainingAmount,
        DateTime RecordedAtUtc,
        string RowVersion,
        List<InstallmentResponse> Installments);

    private sealed record InstallmentResponse(
        int Number,
        DateOnly DueDate,
        decimal Amount,
        string Status,
        DateOnly? PaidOn,
        PaymentResponse? Payment);

    private sealed record PaymentResponse(
        Guid Id,
        decimal Amount,
        DateOnly PaidOn,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record PersonalDebtResponse(
        Guid Id,
        string CounterpartyName,
        decimal Principal,
        decimal RemainingAmount,
        DateOnly BorrowedOn,
        DateOnly? DueDate,
        string? Description,
        string Currency,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion,
        List<RepaymentResponse> Repayments);

    private sealed record RepaymentResponse(
        Guid Id,
        Guid PersonalDebtId,
        decimal Amount,
        string Currency,
        DateOnly PaidOn,
        string? Note,
        string Status,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record CashInflowResponse(
        Guid Id,
        decimal Amount,
        string Currency,
        DateOnly OccurredOn,
        string Source,
        bool? IsEarnedIncome,
        string? Description,
        string Status,
        DebtLinkResponse? DebtLink,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record DebtLinkResponse(string Type, Guid Id);

    private sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
}
