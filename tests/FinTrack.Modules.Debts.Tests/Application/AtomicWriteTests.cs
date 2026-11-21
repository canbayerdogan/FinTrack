using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Domain;
using FinTrack.Modules.Debts.Tests.Support;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using CashInflowsUnitOfWork = FinTrack.Modules.CashInflows.Infrastructure.Persistence.UnitOfWork;
using DebtsUnitOfWork = FinTrack.Modules.Debts.Infrastructure.Persistence.UnitOfWork;

namespace FinTrack.Modules.Debts.Tests.Application;

/// <summary>
/// ADR-0009: borç sahibi handler transaction'ı açar, CashInflows writer kendi
/// DbContext'ini aynı transaction'a enlist eder; commit/rollback birliktedir.
/// </summary>
public sealed class AtomicWriteTests : IAsyncLifetime
{
    private static readonly DateTime NowUtc = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);
    private readonly Guid _userId = Guid.NewGuid();

    private DebtTestDatabase _database = null!;
    private IDebtCashInflowWriter _realWriter = null!;

    public async Task InitializeAsync()
    {
        _database = await DebtTestDatabase.CreateWithCashInflowsAsync();
        _realWriter = DebtHandlerFactory.CreateRealInflowWriter(_database, _userId);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Kredi_ve_para_girisi_ayni_islemde_kaydedilir()
    {
        var factory = CreateFactory();

        var result = await factory.CreateLoan().HandleAsync(
            new CreateLoanCommand(
                30_000m,
                3_000m,
                12,
                new DateOnly(2026, 12, 1),
                "Örnek Bank",
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var inflow = await FindInflowAsync(result.Value.Id);
        Assert.NotNull(inflow);
        Assert.Equal(30_000m, inflow.Amount);
        Assert.Equal(CashInflowSource.BankLoan, inflow.Source);
        Assert.Equal(DebtLinkType.Loan, inflow.DebtType);
        Assert.Equal(Today, inflow.OccurredOn);
        Assert.False(inflow.IsEarnedIncome);
        Assert.Equal(CashInflowStatus.Active, inflow.Status);
    }

    [Fact]
    public async Task Kredi_kaydi_basarisiz_olursa_para_girisi_rollback_olur()
    {
        var factory = CreateFactory();
        var countingUnitOfWork = new CountingUnitOfWork(factory.UnitOfWork)
        {
            ForcedOutcome = SaveChangesOutcome.ConcurrencyConflict,
        };

        var result = await factory.CreateLoan(countingUnitOfWork).HandleAsync(
            new CreateLoanCommand(
                1_000m,
                1_000m,
                1,
                new DateOnly(2026, 12, 1),
                null,
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ConcurrencyConflict", result.Error.Code);
        Assert.False(await AnyLoanAsync());
        Assert.Empty(await _database.CreateInflowContext(_userId).CashInflows.ToListAsync());
    }

    [Fact]
    public async Task Kredi_iptali_basarisiz_olursa_para_girisi_iptali_rollback_olur()
    {
        var factory = CreateFactory();
        var createResult = await factory.CreateLoan().HandleAsync(
            new CreateLoanCommand(
                1_000m,
                1_000m,
                1,
                new DateOnly(2026, 12, 1),
                null,
                null,
                null),
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        var countingUnitOfWork = new CountingUnitOfWork(factory.UnitOfWork)
        {
            ForcedOutcome = SaveChangesOutcome.ConcurrencyConflict,
        };

        var cancelResult = await factory.CancelLoan(countingUnitOfWork).HandleAsync(
            new CancelLoanCommand(createResult.Value.Id, "Vazgeçtim", createResult.Value.RowVersion),
            CancellationToken.None);

        Assert.True(cancelResult.IsFailure);

        await using var loans = _database.CreateContext(_userId);
        var loan = await loans.Loans.SingleAsync();
        Assert.Equal(LoanStatus.Active, loan.Status);

        var inflow = await FindInflowAsync(loan.Id);
        Assert.NotNull(inflow);
        Assert.Equal(CashInflowStatus.Active, inflow.Status);
    }

    [Fact]
    public async Task Kisisel_borc_ve_para_girisi_ayni_islemde_kaydedilir()
    {
        var factory = CreateFactory();

        var result = await factory.CreatePersonalDebt().HandleAsync(
            new CreatePersonalDebtCommand(
                "Ahmet",
                5_000m,
                Today.AddDays(-10),
                Today.AddDays(20),
                "Acil ihtiyaç",
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var inflow = await FindInflowAsync(result.Value.Id);
        Assert.NotNull(inflow);
        Assert.Equal(CashInflowSource.PersonalBorrowing, inflow.Source);
        Assert.Equal(DebtLinkType.PersonalDebt, inflow.DebtType);
        Assert.Equal(Today.AddDays(-10), inflow.OccurredOn);
        Assert.False(inflow.IsEarnedIncome);
    }

    [Fact]
    public async Task Enlist_sirada_owner_ve_writer_ayni_fiziksel_transaction_a_bagli()
    {
        await using var debtsDb = _database.CreateContext(_userId);
        await using var inflowsDb = _database.CreateInflowContext(_userId);

        var connection = debtsDb.Database.GetDbConnection();

        // Fixture kurulumu değil, iki gerçek DbContext'in çalışma anındaki bağlantısı.
        Assert.Same(connection, inflowsDb.Database.GetDbConnection());

        await using var transaction = await connection.BeginTransactionAsync();
        var scope = new SqliteWriteTransactionScope(transaction);
        var ownerUow = new DebtsUnitOfWork(debtsDb);
        var writerUow = new CashInflowsUnitOfWork(inflowsDb);

        ownerUow.Enlist(scope);
        writerUow.Enlist(scope);

        Assert.Same(transaction, scope.Transaction);
        Assert.Same(transaction, debtsDb.Database.CurrentTransaction?.GetDbTransaction());
        Assert.Same(transaction, inflowsDb.Database.CurrentTransaction?.GetDbTransaction());

        // Enlist sonrası bağlantı/transaction kullanılabilir durumda kalmalı.
        await debtsDb.Database.ExecuteSqlRawAsync("SELECT 1");
        await inflowsDb.Database.ExecuteSqlRawAsync("SELECT 1");
    }

    private DebtHandlerFactory CreateFactory() =>
        new(_database.CreateContext(_userId), _userId, NowUtc, inflowWriter: _realWriter);

    private async Task<CashInflow?> FindInflowAsync(Guid debtLinkId)
    {
        await using var inflows = _database.CreateInflowContext(_userId);
        return await inflows.CashInflows
            .SingleOrDefaultAsync(inflow => inflow.DebtLinkId == debtLinkId);
    }

    private async Task<bool> AnyLoanAsync()
    {
        await using var loans = _database.CreateContext(_userId);
        return await loans.Loans.AnyAsync();
    }
}
