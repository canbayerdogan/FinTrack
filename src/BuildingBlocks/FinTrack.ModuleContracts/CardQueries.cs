namespace FinTrack.ModuleContracts;

/// <summary>
/// Kart doğrulaması için gereken asgari kart bilgisi.
/// Kartın sahibi CreditCards modülüdür; Expenses yalnızca bu referansı görür.
/// </summary>
public sealed record CardReference(Guid Id, string Name, bool IsActive);

/// <summary>
/// Kart sahipliği ve durum sorgusu. Uygulaması CreditCards modülündedir.
/// Expenses, kart alışverişinde bu sözleşme ile kartı doğrular; tabloya erişemez.
/// </summary>
public interface ICardQuery
{
    Task<CardReference?> GetCardAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);
}

/// <summary>
/// Bir kartın <b>tüm zamanlar</b> aktif harcama ve iade toplamları (docs/06 §6.3).
/// Yalnızca iptal edilmemiş harcamalar ve iptal edilmemiş iadeler sayılır.
/// Uygulaması, verinin sahibi olan Expenses modülündedir (ADR-0008).
/// </summary>
/// <remarks>
/// Dönem/<c>asOf</c> filtresi bilinçli olarak yoktur; kart borcu tüm zamanlar
/// üzerinden hesaplanır. Ekstre karşılaştırması için tarih kesitli sorgu
/// slice 4'te ayrı bir sözleşme olarak eklenir.
/// </remarks>
public sealed record CardExpenseTotals(decimal Expenses, decimal Refunds);

public interface ICardExpenseQuery
{
    Task<CardExpenseTotals> GetCardTotalsAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken);
}
