using FinTrack.ModuleContracts;
using FinTrack.Modules.Reporting.Application.Reporting;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.Reporting.Tests;

/// <summary>
/// Slice 8 rapor handler'ları: sözleşmeler fake'lenir; Reporting'in başka
/// modüle ait veriye yalnızca ModuleContracts üzerinden baktığı doğrulanır.
/// </summary>
public sealed class ReportingHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestCurrentUser _currentUser = new(UserId);
    private readonly TestClock _clock = new(NowUtc);
    private readonly FakeExpenseReportQuery _expenses = new();
    private readonly FakeCashInflowReportQuery _inflows = new();
    private readonly FakeCardReportQuery _cards = new();
    private readonly FakeDebtReportQuery _debts = new();

    [Fact]
    public async Task Aylik_ozet_dokuman_02_ornegiyle_birebir_eslesir()
    {
        _expenses.Totals = new ExpenseReportTotals(
            50_000m,
            2_000m,
            48_000m,
            [new ExpenseCategoryTotal(Guid.NewGuid(), "Market", 18_000m)]);
        _inflows.Totals = new CashInflowReportTotals(
            80_000m,
            35_000m,
            [
                new CashInflowSourceTotal("BankLoan", 30_000m),
                new CashInflowSourceTotal("CashAdvance", 5_000m),
            ],
            1_000m,
            1);

        var handler = new GetMonthlyReportHandler(_currentUser, _expenses, _inflows);

        var result = await handler.HandleAsync(
            new GetMonthlyReportQuery(2026, 11), CancellationToken.None);

        Assert.True(result.IsSuccess, $"{result.Error.Code}: {result.Error.Message}");
        var report = result.Value;
        Assert.Equal(new DateOnly(2026, 11, 1), report.Period.From);
        Assert.Equal(new DateOnly(2026, 11, 30), report.Period.To);
        Assert.Equal("Europe/Istanbul", report.Period.TimeZone);
        Assert.Equal("TRY", report.Currency);
        Assert.Equal(80_000m, report.EarnedIncome);
        Assert.Equal(35_000m, report.OtherInflows.Total);
        Assert.Equal(2, report.OtherInflows.Items.Count);
        Assert.Equal(1_000m, report.UnknownInflows.Total);
        Assert.Equal(1, report.UnknownInflows.Count);
        Assert.Equal(50_000m, report.Expense.Gross);
        Assert.Equal(2_000m, report.Expense.Refunds);
        Assert.Equal(48_000m, report.Expense.Net);
        Assert.Equal(32_000m, report.IncomeExpenseDifference);
        Assert.False(report.IsBankBalance);
        Assert.Equal("Gelir−gider farkı banka bakiyesi değildir.", report.Disclaimer);
        Assert.Single(report.Categories);

        Assert.Equal(UserId, _expenses.UserId);
        Assert.Equal(new DateOnly(2026, 11, 1), _expenses.From);
        Assert.Equal(new DateOnly(2026, 11, 30), _expenses.To);
        Assert.Equal(UserId, _inflows.UserId);
        Assert.Equal(_expenses.From, _inflows.From);
        Assert.Equal(_expenses.To, _inflows.To);
    }

    [Fact]
    public async Task Artik_yili_ve_tek_sayili_ay_donemi_dogru_uretir()
    {
        var handler = new GetMonthlyReportHandler(
            new TestCurrentUser(UserId, "UTC"), _expenses, _inflows);

        var result = await handler.HandleAsync(
            new GetMonthlyReportQuery(2028, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2028, 2, 29), result.Value.Period.To);
        Assert.Equal("UTC", result.Value.Period.TimeZone);
    }

    [Theory]
    [InlineData(1999, 11, "InvalidYear")]
    [InlineData(2101, 11, "InvalidYear")]
    [InlineData(2026, 0, "InvalidMonth")]
    [InlineData(2026, 13, "InvalidMonth")]
    public async Task Gecersiz_donem_validation_doner(int year, int month, string expectedCode)
    {
        var handler = new GetMonthlyReportHandler(_currentUser, _expenses, _inflows);

        var result = await handler.HandleAsync(
            new GetMonthlyReportQuery(year, month), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Borc_toplami_negatif_karti_diger_borclardan_dusmez()
    {
        var debtCardId = Guid.NewGuid();
        var creditCardId = Guid.NewGuid();
        _cards.DebtReport = new CardDebtReport(
            [
                new CardDebtEntry(debtCardId, "Platinum", 4_200m, false),
                new CardDebtEntry(creditCardId, "Bonus", -800m, true),
            ],
            800m);
        _debts.Loans = [new LoanDebtEntry(Guid.NewGuid(), "Örnek Bank", 30_000m)];
        _debts.PersonalDebts =
        [new PersonalDebtEntry(Guid.NewGuid(), "Ahmet", 4_000m)];

        var handler = new GetDebtSummaryHandler(_currentUser, _clock, _cards, _debts);

        var result = await handler.HandleAsync(new GetDebtSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, $"{result.Error.Code}: {result.Error.Message}");
        var report = result.Value;
        Assert.Equal(38_200m, report.TotalDebt);
        Assert.Equal(800m, report.CardCreditTotal);
        Assert.Equal(2, report.Cards.Count);
        Assert.True(report.Cards.Single(card => card.CardId == creditCardId).IsInCredit);
        Assert.Equal(30_000m, Assert.Single(report.Loans).RemainingAmount);
        Assert.Equal(4_000m, Assert.Single(report.PersonalDebts).RemainingAmount);
        Assert.Equal(NowUtc, report.AsOfUtc);
        Assert.Equal(new DateOnly(2026, 11, 14), _cards.AsOfDate);
        Assert.Equal("Europe/Istanbul", _cards.TimeZoneId);
    }

    [Fact]
    public async Task Yaklasan_odemeler_taksit_ve_ekstreleri_birlestirir_siralar()
    {
        var loanId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var statementId = Guid.NewGuid();
        _debts.UpcomingInstallments =
        [
            new LoanInstallmentDue(loanId, 1, new DateOnly(2026, 12, 1), 3_000m),
        ];
        _cards.StatementDues =
        [
            new CardStatementDue(cardId, statementId, new DateOnly(2026, 11, 20), 6_500m),
        ];

        var handler = new GetUpcomingPaymentsHandler(_currentUser, _clock, _cards, _debts);

        var result = await handler.HandleAsync(
            new GetUpcomingPaymentsQuery(30), CancellationToken.None);

        Assert.True(result.IsSuccess, $"{result.Error.Code}: {result.Error.Message}");
        var report = result.Value;
        Assert.Equal(new DateOnly(2026, 11, 14), report.From);
        Assert.Equal(new DateOnly(2026, 12, 14), report.To);
        Assert.Equal(2, report.Items.Count);
        Assert.Equal("CardStatement", report.Items[0].Type);
        Assert.Equal(statementId, report.Items[0].StatementId);
        Assert.Equal("LoanInstallment", report.Items[1].Type);
        Assert.Equal(loanId, report.Items[1].LoanId);
        Assert.Equal(1, report.Items[1].InstallmentNumber);
        Assert.Equal(new DateOnly(2026, 11, 14), _cards.From);
        Assert.Equal(new DateOnly(2026, 12, 14), _cards.To);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public async Task Gecersiz_gun_sayisi_validation_doner(int days)
    {
        var handler = new GetUpcomingPaymentsHandler(_currentUser, _clock, _cards, _debts);

        var result = await handler.HandleAsync(
            new GetUpcomingPaymentsQuery(days), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InvalidDays", result.Error.Code);
    }

    private sealed record TestCurrentUser(Guid UserId, string TimeZone = UserTime.DefaultTimeZoneId)
        : ICurrentUser
    {
        Guid? ICurrentUser.UserId => UserId;
        public bool IsAuthenticated => true;
        public string PreferredTimeZone => TimeZone;
        public Guid RequireUserId() => UserId;
    }

    private sealed record TestClock(DateTime UtcNow) : IClock;

    private sealed class FakeExpenseReportQuery : IExpenseReportQuery
    {
        public ExpenseReportTotals Totals { get; set; } = new(0m, 0m, 0m, []);
        public Guid UserId { get; private set; }
        public DateOnly From { get; private set; }
        public DateOnly To { get; private set; }

        public Task<ExpenseReportTotals> GetMonthlyAsync(
            Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            UserId = userId;
            From = from;
            To = to;
            return Task.FromResult(Totals);
        }
    }

    private sealed class FakeCashInflowReportQuery : ICashInflowReportQuery
    {
        public CashInflowReportTotals Totals { get; set; } =
            new(0m, 0m, [], 0m, 0);
        public Guid UserId { get; private set; }
        public DateOnly From { get; private set; }
        public DateOnly To { get; private set; }

        public Task<CashInflowReportTotals> GetMonthlyAsync(
            Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            UserId = userId;
            From = from;
            To = to;
            return Task.FromResult(Totals);
        }
    }

    private sealed class FakeCardReportQuery : ICardReportQuery
    {
        public CardDebtReport DebtReport { get; set; } = new([], 0m);
        public IReadOnlyList<CardStatementDue> StatementDues { get; set; } = [];
        public DateOnly AsOfDate { get; private set; }
        public string? TimeZoneId { get; private set; }
        public DateOnly From { get; private set; }
        public DateOnly To { get; private set; }

        public Task<CardDebtReport> GetDebtAsync(
            Guid userId,
            DateOnly asOfDate,
            string timeZoneId,
            CancellationToken cancellationToken)
        {
            AsOfDate = asOfDate;
            TimeZoneId = timeZoneId;
            return Task.FromResult(DebtReport);
        }

        public Task<IReadOnlyList<CardStatementDue>> GetStatementDuesAsync(
            Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            From = from;
            To = to;
            return Task.FromResult(StatementDues);
        }
    }

    private sealed class FakeDebtReportQuery : IDebtReportQuery
    {
        public IReadOnlyList<LoanDebtEntry> Loans { get; set; } = [];
        public IReadOnlyList<PersonalDebtEntry> PersonalDebts { get; set; } = [];
        public IReadOnlyList<LoanInstallmentDue> UpcomingInstallments { get; set; } = [];

        public Task<IReadOnlyList<LoanDebtEntry>> GetLoansAsync(
            Guid userId, CancellationToken cancellationToken) => Task.FromResult(Loans);

        public Task<IReadOnlyList<PersonalDebtEntry>> GetPersonalDebtsAsync(
            Guid userId, CancellationToken cancellationToken) => Task.FromResult(PersonalDebts);

        public Task<IReadOnlyList<LoanInstallmentDue>> GetUpcomingInstallmentsAsync(
            Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult(UpcomingInstallments);
    }
}
