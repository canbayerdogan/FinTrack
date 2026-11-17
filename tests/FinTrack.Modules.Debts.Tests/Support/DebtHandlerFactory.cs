using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Infrastructure.Persistence;
using FinTrack.Modules.CashInflows.Infrastructure.Repositories;
using FinTrack.Modules.Debts.Application.Abstractions;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Application.Reporting;
using FinTrack.Modules.Debts.Infrastructure;
using FinTrack.Modules.Debts.Infrastructure.Persistence;
using FinTrack.Modules.Debts.Infrastructure.Queries;
using FinTrack.Modules.Debts.Infrastructure.Repositories;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;
using CashInflowsUnitOfWork = FinTrack.Modules.CashInflows.Infrastructure.Persistence.UnitOfWork;
using DebtsUnitOfWork = FinTrack.Modules.Debts.Infrastructure.Persistence.UnitOfWork;

namespace FinTrack.Modules.Debts.Tests.Support;

/// <summary>
/// Handler'ları gerçek repository/sorgu/UnitOfWork ile, sabit kullanıcı ve saatle
/// kurar. Yalnızca modüller arası writer sahtelenebilir; para girişi tarafı
/// gerçek altyapı üzerinden SQLite'a yazılabilir.
/// </summary>
public sealed class DebtHandlerFactory
{
    private readonly ICurrentUser _currentUser;
    private readonly TestClock _clock;

    public DebtHandlerFactory(
        DebtsDbContext db,
        Guid userId,
        DateTime nowUtc,
        string preferredTimeZone = UserTime.DefaultTimeZoneId,
        IDebtCashInflowWriter? inflowWriter = null)
    {
        _currentUser = new TestCurrentUser(userId, preferredTimeZone);
        _clock = new TestClock(nowUtc);
        Loans = new LoanRepository(db);
        LoanQueries = new LoanQueries(db);
        Debts = new PersonalDebtRepository(db);
        DebtQueries = new PersonalDebtQueries(db);
        SummaryQueries = new DebtSummaryQueries(db);
        UnitOfWork = new DebtsUnitOfWork(db);
        WriteTransactions = new SqliteWriteTransactionScopeFactory(db.Database.GetDbConnection());
        FakeInflowWriter = new FakeDebtCashInflowWriter();
        RealInflowWriter = inflowWriter;
    }

    internal LoanRepository Loans { get; }

    internal LoanQueries LoanQueries { get; }

    internal PersonalDebtRepository Debts { get; }

    internal PersonalDebtQueries DebtQueries { get; }

    internal DebtSummaryQueries SummaryQueries { get; }

    internal DebtsUnitOfWork UnitOfWork { get; }

    internal SqliteWriteTransactionScopeFactory WriteTransactions { get; }

    public FakeDebtCashInflowWriter FakeInflowWriter { get; }

    private IDebtCashInflowWriter? RealInflowWriter { get; }

    private IDebtCashInflowWriter Writer => RealInflowWriter ?? FakeInflowWriter;

    public static IDebtCashInflowWriter CreateRealInflowWriter(DebtTestDatabase database, Guid userId)
    {
        var inflowContext = database.CreateInflowContext(userId);
        return new DebtCashInflowWriter(
            new CashInflowRepository(inflowContext),
            new CashInflowsUnitOfWork(inflowContext));
    }

    public CreateLoanHandler CreateLoan(CountingUnitOfWork? unitOfWork = null) =>
        new(_currentUser, Loans, (IUnitOfWork?)unitOfWork ?? UnitOfWork, WriteTransactions, Writer, _clock);

    public CancelLoanHandler CancelLoan(CountingUnitOfWork? unitOfWork = null) =>
        new(_currentUser, Loans, (IUnitOfWork?)unitOfWork ?? UnitOfWork, WriteTransactions, Writer, _clock);

    public PayInstallmentHandler PayInstallment() =>
        new(_currentUser, Loans, UnitOfWork, _clock);

    public CancelInstallmentPaymentHandler CancelInstallmentPayment() =>
        new(_currentUser, Loans, UnitOfWork, _clock);

    public ListLoansHandler ListLoans() => new(_currentUser, LoanQueries);

    public GetLoanHandler GetLoan() => new(_currentUser, LoanQueries);

    public CreatePersonalDebtHandler CreatePersonalDebt(CountingUnitOfWork? unitOfWork = null) =>
        new(_currentUser, Debts, (IUnitOfWork?)unitOfWork ?? UnitOfWork, WriteTransactions, Writer, _clock);

    public CancelPersonalDebtHandler CancelPersonalDebt(CountingUnitOfWork? unitOfWork = null) =>
        new(_currentUser, Debts, (IUnitOfWork?)unitOfWork ?? UnitOfWork, WriteTransactions, Writer, _clock);

    public AddRepaymentHandler AddRepayment() =>
        new(_currentUser, Debts, UnitOfWork, _clock);

    public CancelRepaymentHandler CancelRepayment() =>
        new(_currentUser, Debts, UnitOfWork, _clock);

    public ListPersonalDebtsHandler ListPersonalDebts() => new(_currentUser, DebtQueries);

    public GetPersonalDebtHandler GetPersonalDebt() => new(_currentUser, DebtQueries);

    public ListRepaymentsHandler ListRepayments() => new(_currentUser, DebtQueries);

    public LoanDebtQuery LoanDebtQuery() => new(SummaryQueries);
}
