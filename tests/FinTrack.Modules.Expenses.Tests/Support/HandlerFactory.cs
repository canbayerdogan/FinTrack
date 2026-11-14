using FinTrack.ModuleContracts;
using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.Modules.Expenses.Infrastructure;
using FinTrack.Modules.Expenses.Infrastructure.Persistence;
using FinTrack.Modules.Expenses.Infrastructure.Queries;
using FinTrack.Modules.Expenses.Infrastructure.Repositories;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Tests.Support;

/// <summary>Handler'ları gerçek repository/UnitOfWork ile, sabit kullanıcı ve saatle kurar.</summary>
public sealed class HandlerFactory
{
    private readonly ICurrentUser _currentUser;
    private readonly TestClock _clock;
    private readonly ExpenseReferenceService _references;

    public HandlerFactory(
        ExpensesDbContext db,
        Guid userId,
        DateTime nowUtc,
        params ICardQuery[] cardQueries)
    {
        _currentUser = new TestCurrentUser(userId);
        _clock = new TestClock(nowUtc);
        Categories = new CategoryRepository(db);
        Expenses = new ExpenseRepository(db);
        CategoryQueries = new CategoryQueries(db);
        UnitOfWork = new UnitOfWork(db);
        Transactions = new TransactionScopeFactory(db);
        _references = new ExpenseReferenceService(Categories, cardQueries);
    }

    internal CategoryRepository Categories { get; }

    internal ExpenseRepository Expenses { get; }

    internal CategoryQueries CategoryQueries { get; }

    internal UnitOfWork UnitOfWork { get; }

    internal TransactionScopeFactory Transactions { get; }

    public CreateCategoryHandler CreateCategory() =>
        new(_currentUser, Categories, UnitOfWork, _clock);

    public UpdateCategoryHandler UpdateCategory() =>
        new(_currentUser, Categories, UnitOfWork);

    public ArchiveCategoryHandler ArchiveCategory() =>
        new(_currentUser, Categories, UnitOfWork, _clock);

    public GetCategoriesHandler GetCategories() =>
        new(_currentUser, CategoryQueries);

    public CreateExpenseHandler CreateExpense() =>
        new(_currentUser, Expenses, UnitOfWork, _references, _clock);

    public UpdateExpenseHandler UpdateExpense() =>
        new(_currentUser, Expenses, UnitOfWork, Transactions, _references, _clock);

    public CancelExpenseHandler CancelExpense() =>
        new(_currentUser, Expenses, UnitOfWork, Transactions, _clock);

    public AddRefundHandler AddRefund() =>
        new(_currentUser, Expenses, UnitOfWork, Transactions, _clock);

    public CancelRefundHandler CancelRefund() =>
        new(_currentUser, Expenses, UnitOfWork, Transactions, _clock);
}

/// <summary>Kart modülü varken aktif/arşivli kart senaryolarını taklit eder.</summary>
public sealed class FakeCardQuery(params CardReference[] cards) : ICardQuery
{
    public CardReference? LastRequested { get; private set; }

    public Task<CardReference?> GetCardAsync(Guid userId, Guid cardId, CancellationToken cancellationToken)
    {
        LastRequested = cards.FirstOrDefault(card => card.Id == cardId);
        return Task.FromResult(LastRequested);
    }
}
