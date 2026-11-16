namespace FinTrack.Modules.CreditCards.Application.Common;

/// <summary>Sayfalanmış liste yanıtı (docs/06 §1.6).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
