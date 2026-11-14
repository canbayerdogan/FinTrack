using FinTrack.ModuleContracts;
using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Expenses;

/// <summary>
/// Harcamanın kategori ve kart referanslarını doğrular. Kategori aynı modüldedir;
/// kart yalnızca <see cref="ICardQuery"/> ile okunur (modüller arası FK yok).
/// Güncellemede mevcut referans korunuyorsa arşivli kabul edilir (docs/07 §4).
/// </summary>
public sealed class ExpenseReferenceService(
    ICategoryRepository categories,
    IEnumerable<ICardQuery> cardQueries)
{
    private readonly ICardQuery? _cardQuery = cardQueries.FirstOrDefault();

    public async Task<Result<Category>> ResolveCategoryAsync(
        Guid userId,
        Guid categoryId,
        Guid? currentCategoryId,
        CancellationToken cancellationToken)
    {
        var category = await categories.FindAsync(userId, categoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<Category>(Error.Validation("ExpenseCategoryNotFound", "Kategori bulunamadı."));
        }

        if (category.IsArchived && categoryId != currentCategoryId)
        {
            return Result.Failure<Category>(
                Error.Validation("ExpenseCategoryArchived", "Arşivlenmiş kategoriye yeni harcama eklenemez."));
        }

        return Result.Success(category);
    }

    public async Task<Result> ResolveCardAsync(
        Guid userId,
        PaymentMethod paymentMethod,
        Guid? cardId,
        Guid? currentCardId,
        CancellationToken cancellationToken)
    {
        if (paymentMethod != PaymentMethod.CreditCard || cardId is null || cardId == Guid.Empty)
        {
            // Eksik/izin verilmeyen kart alanı domain kurallarında yakalanır.
            return Result.Success();
        }

        if (_cardQuery is null)
        {
            return Result.Failure(
                Error.Validation("ExpenseCardNotFound", "Kart bulunamadı."));
        }

        var card = await _cardQuery.GetCardAsync(userId, cardId.Value, cancellationToken);
        if (card is null)
        {
            return Result.Failure(
                Error.Validation("ExpenseCardNotFound", "Kart bulunamadı."));
        }

        if (!card.IsActive && cardId != currentCardId)
        {
            return Result.Failure(
                Error.Validation("ExpenseCardArchived", "Arşivlenmiş karta yeni harcama eklenemez."));
        }

        return Result.Success();
    }
}
