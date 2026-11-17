using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Tests.Support;
using Xunit;

namespace FinTrack.Modules.Debts.Tests.Application;

public sealed class PersonalDebtHandlerTests : IAsyncLifetime
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
    public async Task Olusturma_para_girisini_borclanma_tarihiyle_yazar()
    {
        var result = await CreateDebtAsync(5_000m);

        Assert.Equal(5_000m, result.RemainingAmount);
        Assert.Empty(result.Repayments);

        var inflow = _factory.FakeInflowWriter.LastCreate;
        Assert.NotNull(inflow);
        Assert.Equal(DebtInflowKind.PersonalDebt, inflow.Kind);
        Assert.Equal(5_000m, inflow.Amount);
        Assert.Equal(Today.AddDays(-10), inflow.OccurredOn);
        Assert.Equal(Today, inflow.Today);
        Assert.Equal(result.Id, inflow.DebtLinkId);
    }

    [Fact]
    public async Task GeriOdeme_ve_iptali_kalan_tutari_gunceller()
    {
        var debt = await CreateDebtAsync(1_000m);

        var added = await _factory.AddRepayment().HandleAsync(
            new AddRepaymentCommand(debt.Id, 400m, Today, "İlk"),
            CancellationToken.None);

        Assert.True(added.IsSuccess, $"{added.Error.Code}: {added.Error.Message}");
        var withRepayment = await GetDebtAsync(debt.Id);
        Assert.Equal(600m, withRepayment.RemainingAmount);

        var canceled = await _factory.CancelRepayment().HandleAsync(
            new CancelRepaymentCommand(debt.Id, added.Value.Id, "Yanlış", added.Value.RowVersion),
            CancellationToken.None);

        Assert.True(canceled.IsSuccess);
        var reloaded = await GetDebtAsync(debt.Id);
        Assert.Equal(1_000m, reloaded.RemainingAmount);
        Assert.Empty(reloaded.Repayments);
    }

    [Fact]
    public async Task Aktif_geriOdeme_varken_iptal_red_edilir()
    {
        var debt = await CreateDebtAsync(1_000m);
        var active = await _factory.AddRepayment().HandleAsync(
            new AddRepaymentCommand(debt.Id, 400m, Today, null),
            CancellationToken.None);
        Assert.True(active.IsSuccess, $"{active.Error.Code}: {active.Error.Message}");

        var result = await _factory.CancelPersonalDebt().HandleAsync(
            new CancelPersonalDebtCommand(debt.Id, "Vazgeçtim", debt.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("StateConflict", result.Error.Code);
    }

    [Fact]
    public async Task Iptal_bagli_para_girisini_de_iptal_eder()
    {
        var debt = await CreateDebtAsync(1_000m);

        var result = await _factory.CancelPersonalDebt().HandleAsync(
            new CancelPersonalDebtCommand(debt.Id, "Vazgeçtim", debt.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cancel = _factory.FakeInflowWriter.LastCancel;
        Assert.NotNull(cancel);
        Assert.Equal(DebtInflowKind.PersonalDebt, cancel.Kind);
        Assert.Equal(debt.Id, cancel.DebtLinkId);
        Assert.Same(_factory.WriteTransactions.LastScope, _factory.FakeInflowWriter.LastCancelScope);
    }

    [Fact]
    public async Task Baska_kullanicinin_borcu_gorunmez()
    {
        var debt = await CreateDebtAsync(1_000m);
        var otherUserId = Guid.NewGuid();
        var otherFactory = new DebtHandlerFactory(
            _database.CreateContext(otherUserId),
            otherUserId,
            NowUtc);

        var result = await otherFactory.GetPersonalDebt().HandleAsync(
            new GetPersonalDebtQuery(debt.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Borc_toplami_iptal_edilenleri_haric_tutar()
    {
        await CreateDebtAsync(1_000m);
        var second = await CreateDebtAsync(500m);
        Assert.True((await _factory.CancelPersonalDebt().HandleAsync(
            new CancelPersonalDebtCommand(second.Id, "Vazgeçtim", second.RowVersion),
            CancellationToken.None)).IsSuccess);

        var total = await _factory.LoanDebtQuery().GetTotalRemainingAsync(_userId, CancellationToken.None);

        Assert.Equal(1_000m, total.Amount);
    }

    private async Task<PersonalDebtDto> CreateDebtAsync(decimal principal)
    {
        var result = await _factory.CreatePersonalDebt().HandleAsync(
            new CreatePersonalDebtCommand(
                "Ahmet",
                principal,
                Today.AddDays(-10),
                Today.AddDays(20),
                "Acil ihtiyaç",
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<PersonalDebtDto> GetDebtAsync(Guid id)
    {
        var result = await _factory.GetPersonalDebt().HandleAsync(
            new GetPersonalDebtQuery(id),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
