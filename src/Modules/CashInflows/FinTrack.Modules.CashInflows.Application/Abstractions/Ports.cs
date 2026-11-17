using FinTrack.ModuleContracts;
using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.Modules.CashInflows.Domain;

namespace FinTrack.Modules.CashInflows.Application.Abstractions;

/// <summary>Para girişi yazma tarafı. Sahiplik her zaman açıkça kontrol edilir.</summary>
public interface ICashInflowRepository
{
    Task<CashInflow?> FindAsync(Guid userId, Guid cashInflowId, CancellationToken cancellationToken);

    Task AddAsync(CashInflow cashInflow, CancellationToken cancellationToken);
}

public interface ICashInflowQueries
{
    Task<PagedResult<CashInflowDto>> ListAsync(
        Guid userId,
        CashInflowFilter filter,
        CancellationToken cancellationToken);

    Task<CashInflowDto?> FindAsync(Guid userId, Guid cashInflowId, CancellationToken cancellationToken);
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

    /// <summary>
    /// ADR-0009: bu modülün DbContext'ini paylaşılan transaction'a bağlar.
    /// <c>null</c> çağrısı bağlantıyı temizler; scope kapandıktan sonra aynı
    /// DbContext'in yeniden kullanılabilmesi için zorunludur.
    /// </summary>
    void Enlist(IWriteTransactionScope? scope);
}
