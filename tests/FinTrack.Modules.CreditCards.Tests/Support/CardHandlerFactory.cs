using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.Modules.CreditCards.Infrastructure;
using FinTrack.Modules.CreditCards.Infrastructure.Persistence;
using FinTrack.Modules.CreditCards.Infrastructure.Queries;
using FinTrack.Modules.CreditCards.Infrastructure.Repositories;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Tests.Support;

/// <summary>
/// Handler'ları gerçek repository/sorgu/UnitOfWork ile, sabit kullanıcı ve saatle
/// kurar. Yalnızca modüller arası <see cref="ICardExpenseQuery"/> sahtelenir;
/// ödeme ve ekstre verisi gerçek altyapı üzerinden SQLite'a yazılır.
/// </summary>
public sealed class CardHandlerFactory
{
    private readonly ICurrentUser _currentUser;
    private readonly TestClock _clock;

    public CardHandlerFactory(
        CreditCardsDbContext db,
        Guid userId,
        DateTime nowUtc,
        string preferredTimeZone = UserTime.DefaultTimeZoneId)
    {
        _currentUser = new TestCurrentUser(userId, preferredTimeZone);
        _clock = new TestClock(nowUtc);
        Cards = new CardRepository(db);
        CardQueries = new CardQueries(db);
        Payments = new CardPaymentRepository(db);
        PaymentQueries = new CardPaymentQueries(db);
        Statements = new StatementRepository(db);
        StatementQueries = new StatementQueries(db);
        UnitOfWork = new UnitOfWork(db);
        Transactions = new TransactionScopeFactory(db);
        ExpenseQuery = new FakeCardExpenseQuery();
        DebtService = new CardDebtService(ExpenseQuery, PaymentQueries);
        Derivation = new StatementDerivation(DebtService, PaymentQueries, StatementQueries);
    }

    internal CardRepository Cards { get; }

    internal CardQueries CardQueries { get; }

    internal CardPaymentRepository Payments { get; }

    internal CardPaymentQueries PaymentQueries { get; }

    internal StatementRepository Statements { get; }

    internal StatementQueries StatementQueries { get; }

    internal UnitOfWork UnitOfWork { get; }

    internal TransactionScopeFactory Transactions { get; }

    internal CardDebtService DebtService { get; }

    internal StatementDerivation Derivation { get; }

    public FakeCardExpenseQuery ExpenseQuery { get; }

    public CreateCardHandler CreateCard() =>
        new(_currentUser, Cards, UnitOfWork, _clock);

    public UpdateCardHandler UpdateCard() =>
        new(_currentUser, Cards, UnitOfWork);

    public ArchiveCardHandler ArchiveCard() =>
        new(_currentUser, Cards, UnitOfWork, _clock);

    public ListCardsHandler ListCards() =>
        new(_currentUser, CardQueries);

    public GetCardHandler GetCard() =>
        new(_currentUser, CardQueries);

    public GetCardBalanceHandler GetCardBalance() =>
        new(_currentUser, CardQueries, DebtService, _clock);

    public CreateCardPaymentHandler CreatePayment() =>
        new(_currentUser, Cards, Payments, UnitOfWork, _clock);

    public CancelCardPaymentHandler CancelPayment() =>
        new(_currentUser, Payments, UnitOfWork, _clock);

    public ListCardPaymentsHandler ListPayments() =>
        new(_currentUser, CardQueries, PaymentQueries);

    public CreateStatementHandler CreateStatement() =>
        new(_currentUser, Cards, Statements, Derivation, Transactions, UnitOfWork, _clock);

    public CancelStatementHandler CancelStatement() =>
        new(_currentUser, Statements, UnitOfWork, _clock);

    public ListStatementsHandler ListStatements() =>
        new(_currentUser, CardQueries, StatementQueries, Derivation);

    public GetStatementHandler GetStatement() =>
        new(_currentUser, StatementQueries, Derivation);
}

/// <summary>Kart borcu testlerinde Expenses tarafını taklit eder.</summary>
public sealed class FakeCardExpenseQuery : ICardExpenseQuery
{
    /// <summary>Tüm zamanlar toplamı.</summary>
    public CardExpenseTotals Totals { get; set; } = new(0m, 0m);

    /// <summary>Kesitli (<c>asOf</c>) toplam; bakiye/ekstre hesabı bunu kullanır.</summary>
    public CardExpenseTotals AsOfTotals { get; set; } = new(0m, 0m);

    /// <summary>Kesite göre farklı toplam döndürmek için opsiyonel üretici.</summary>
    public Func<DateOnly, CardExpenseTotals>? AsOfTotalsFactory { get; set; }

    public Guid? LastCardId { get; private set; }

    public DateOnly? LastAsOf { get; private set; }

    public Task<CardExpenseTotals> GetCardTotalsAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        LastCardId = cardId;
        return Task.FromResult(Totals);
    }

    public Task<CardExpenseTotals> GetCardTotalsAsOfAsync(
        Guid userId,
        Guid cardId,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        LastCardId = cardId;
        LastAsOf = asOfDate;
        return Task.FromResult(AsOfTotalsFactory?.Invoke(asOfDate) ?? AsOfTotals);
    }
}
