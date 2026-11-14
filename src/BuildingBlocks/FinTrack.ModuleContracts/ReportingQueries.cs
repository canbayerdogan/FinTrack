using FinTrack.SharedKernel;

namespace FinTrack.ModuleContracts;

/// <summary>
/// Modüller arası <b>senkron okuma</b> sözleşmeleri.
/// </summary>
/// <remarks>
/// Kurallar:
/// <list type="bullet">
/// <item>Bu arayüzlerin implementasyonu, verinin sahibi olan modülün Application katmanındadır.</item>
/// <item>Yalnızca okuma amaçlıdır. Yazma için modülün kendi Command'ı kullanılır.</item>
/// <item>Modüller birbirinin DbContext'ine veya entity'sine erişemez; yalnızca bu sözleşmeleri görür.</item>
/// </list>
/// Reporting modülü bu arayüzlerin tek tüketicisidir.
/// </remarks>
public interface IExpenseSummaryQuery
{
    Task<Money> GetTotalAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public interface ICashInflowSummaryQuery
{
    Task<Money> GetEarnedIncomeTotalAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<Money> GetOtherInflowTotalAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public interface ICardDebtQuery
{
    /// <summary>
    /// Kart borçlarının toplamı. Nakit avans borcu bu toplama <b>bir kez</b> dahildir.
    /// </summary>
    Task<Money> GetTotalCardDebtAsync(Guid userId, CancellationToken cancellationToken);
}

public interface ILoanDebtQuery
{
    Task<Money> GetTotalRemainingAsync(Guid userId, CancellationToken cancellationToken);
}
