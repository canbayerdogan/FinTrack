using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Infrastructure;
using FinTrack.Modules.CreditCards.Infrastructure.Persistence;
using FinTrack.Modules.CreditCards.Infrastructure.Queries;
using FinTrack.Modules.CreditCards.Infrastructure.Repositories;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Tests.Support;

/// <summary>Handler'ları gerçek repository/UnitOfWork ile, sabit kullanıcı ve saatle kurar.</summary>
public sealed class CardHandlerFactory
{
    private readonly ICurrentUser _currentUser;
    private readonly TestClock _clock;

    public CardHandlerFactory(CreditCardsDbContext db, Guid userId, DateTime nowUtc)
    {
        _currentUser = new TestCurrentUser(userId);
        _clock = new TestClock(nowUtc);
        Cards = new CardRepository(db);
        CardQueries = new CardQueries(db);
        UnitOfWork = new UnitOfWork(db);
        ExpenseQuery = new FakeCardExpenseQuery();
    }

    internal CardRepository Cards { get; }

    internal CardQueries CardQueries { get; }

    internal UnitOfWork UnitOfWork { get; }

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
        new(_currentUser, CardQueries, ExpenseQuery, _clock);
}

/// <summary>Kart borcu testlerinde Expenses tarafını taklit eder.</summary>
public sealed class FakeCardExpenseQuery : ICardExpenseQuery
{
    public CardExpenseTotals Totals { get; set; } = new(0m, 0m);

    public Guid? LastCardId { get; private set; }

    public Task<CardExpenseTotals> GetCardTotalsAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        LastCardId = cardId;
        return Task.FromResult(Totals);
    }
}
