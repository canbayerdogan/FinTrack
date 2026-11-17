using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Tests.Support;
using Xunit;

namespace FinTrack.Modules.Debts.Tests.Application;

public sealed class LoanHandlerTests : IAsyncLifetime
{
    private static readonly DateTime NowUtc = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);
    private readonly Guid _userId = Guid.NewGuid();

    private DebtTestDatabase _database = null!;
    private DebtHandlerFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _database = await DebtTestDatabase.CreateAsync();
        _factory = new DebtHandlerFactory(_database.CreateContext(_userId), _userId, NowUtc);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Olusturma_para_girisini_bugun_tarihiyle_yazar()
    {
        var result = await _factory.CreateLoan().HandleAsync(
            new CreateLoanCommand(
                30_000m,
                3_000m,
                12,
                new DateOnly(2026, 12, 1),
                "Örnek Bank",
                "İhtiyaç kredisi",
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.Installments.Count);
        Assert.Equal(36_000m, result.Value.TotalRepayment);
        Assert.Equal(36_000m, result.Value.RemainingAmount);

        var inflow = _factory.FakeInflowWriter.LastCreate;
        Assert.NotNull(inflow);
        Assert.Equal(DebtInflowKind.Loan, inflow.Kind);
        Assert.Equal(30_000m, inflow.Amount);
        Assert.Equal(Today, inflow.OccurredOn);
        Assert.Equal(Today, inflow.Today);
        Assert.Equal(result.Value.Id, inflow.DebtLinkId);
        Assert.NotEqual(Guid.Empty, inflow.DebtLinkId);
    }

    [Fact]
    public async Task Taksit_odeme_ve_iptali_kalan_tutari_gunceller()
    {
        var loan = await CreateLoanAsync(3);
        var payResult = await _factory.PayInstallment().HandleAsync(
            new PayInstallmentCommand(loan.Id, 1, Today, loan.RowVersion),
            CancellationToken.None);

        Assert.True(payResult.IsSuccess, $"{payResult.Error.Code}: {payResult.Error.Message}");
        Assert.Equal(2_000m, payResult.Value.RemainingAmount);

        var payment = payResult.Value.Installments.Single(i => i.Number == 1).Payment;
        Assert.NotNull(payment);

        var cancelResult = await _factory.CancelInstallmentPayment().HandleAsync(
            new CancelInstallmentPaymentCommand(
                loan.Id,
                1,
                payment!.Id,
                "Yanlış ödeme",
                payment.RowVersion),
            CancellationToken.None);

        Assert.True(cancelResult.IsSuccess);
        var reloaded = await GetLoanAsync(loan.Id);
        Assert.Equal(3_000m, reloaded.RemainingAmount);
        Assert.Null(reloaded.Installments.Single(i => i.Number == 1).Payment);
    }

    [Fact]
    public async Task Yanlis_rowVersion_ile_odeme_red_edilir()
    {
        var loan = await CreateLoanAsync(2);

        var result = await _factory.PayInstallment().HandleAsync(
            new PayInstallmentCommand(loan.Id, 1, Today, "AAAAAAAAAAA="),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ConcurrencyConflict", result.Error.Code);
    }

    [Fact]
    public async Task Iptal_bagli_para_girisini_de_iptal_eder()
    {
        var loan = await CreateLoanAsync(2);

        var result = await _factory.CancelLoan().HandleAsync(
            new CancelLoanCommand(loan.Id, "Vazgeçtim", loan.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancel = _factory.FakeInflowWriter.LastCancel;
        Assert.NotNull(cancel);
        Assert.Equal(DebtInflowKind.Loan, cancel.Kind);
        Assert.Equal(loan.Id, cancel.DebtLinkId);
        Assert.Equal("Vazgeçtim", cancel.Reason);
        Assert.Same(_factory.WriteTransactions.LastScope, _factory.FakeInflowWriter.LastCancelScope);
    }

    [Fact]
    public async Task Baska_kullanicinin_kredisi_gorunmez()
    {
        var loan = await CreateLoanAsync(1);
        var otherUserId = Guid.NewGuid();
        var otherFactory = new DebtHandlerFactory(
            _database.CreateContext(otherUserId),
            otherUserId,
            NowUtc);

        var result = await otherFactory.GetLoan().HandleAsync(
            new GetLoanQuery(loan.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    private async Task<LoanDto> CreateLoanAsync(int count)
    {
        var result = await _factory.CreateLoan().HandleAsync(
            new CreateLoanCommand(
                1_000m * count,
                1_000m,
                count,
                new DateOnly(2026, 12, 1),
                null,
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<LoanDto> GetLoanAsync(Guid id)
    {
        var result = await _factory.GetLoan().HandleAsync(new GetLoanQuery(id), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
