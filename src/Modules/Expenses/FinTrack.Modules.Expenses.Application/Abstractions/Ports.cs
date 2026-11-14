using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.Modules.Expenses.Domain;

namespace FinTrack.Modules.Expenses.Application.Abstractions;

/// <summary>Kategori yazma tarafı. Filtre uygulanmış olsa da sahiplik her zaman açıkça kontrol edilir.</summary>
public interface ICategoryRepository
{
    Task<Category?> FindAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(
        Guid userId,
        string normalizedName,
        Guid? excludeCategoryId,
        CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);
}

/// <summary>
/// Harcama yazma tarafı. İade koleksiyonu aggregate ile birlikte yüklenir;
/// iade toplamı her zaman aynı tutarlılık sınırında doğrulanır.
/// </summary>
public interface IExpenseRepository
{
    Task<Expense?> FindAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken);

    Task AddAsync(Expense expense, CancellationToken cancellationToken);

    /// <summary>
    /// Yeni iadeyi açıkça <c>Added</c> olarak izler. Navigasyondan keşfedilen
    /// anahtarlı varlık EF tarafından <c>Modified</c> sanılabilir.
    /// </summary>
    Task AddRefundAsync(Refund refund, CancellationToken cancellationToken);
}

public interface ICategoryQueries
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(
        Guid userId,
        bool includeArchived,
        CancellationToken cancellationToken);
}

public interface IExpenseQueries
{
    Task<PagedResult<ExpenseDto>> ListAsync(
        Guid userId,
        ExpenseFilter filter,
        CancellationToken cancellationToken);

    Task<ExpenseDto?> FindAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken);

    /// <summary>Harcama yoksa <c>null</c>; varsa iadeler (boş olabilir).</summary>
    Task<IReadOnlyList<RefundDto>?> ListRefundsAsync(
        Guid userId,
        Guid expenseId,
        CancellationToken cancellationToken);
}

public enum SaveChangesOutcome
{
    Success = 0,
    ConcurrencyConflict = 1,
    DuplicateKey = 2,
}

/// <summary>
/// EF Core istisnalarını Application sınırında sonuçlara çevirir; handler'lar
/// altyapı tipi görmez (mimari testi bunu zorunlu kılar).
/// </summary>
public interface IUnitOfWork
{
    Task<SaveChangesOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Yazma işlemini atomik tutan altyapı bağımsız işlem sınırı.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface ITransactionScopeFactory
{
    Task<ITransactionScope> BeginSerializableAsync(CancellationToken cancellationToken);
}
