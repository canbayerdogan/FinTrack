using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.CashAdvances;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.Modules.CreditCards.Domain;

namespace FinTrack.Modules.CreditCards.Application.Abstractions;

/// <summary>Kart yazma tarafı. Filtre uygulanmış olsa da sahiplik her zaman açıkça kontrol edilir.</summary>
public interface ICardRepository
{
    Task<CreditCard?> FindAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);

    Task AddAsync(CreditCard card, CancellationToken cancellationToken);
}

public interface ICardQueries
{
    Task<IReadOnlyList<CardDto>> ListAsync(
        Guid userId,
        CardFilter filter,
        CancellationToken cancellationToken);

    Task<CardDto?> FindAsync(Guid userId, Guid cardId, CancellationToken cancellationToken);
}

/// <summary>Kart ödemesi yazma tarafı.</summary>
public interface ICardPaymentRepository
{
    Task<CardPayment?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid paymentId,
        CancellationToken cancellationToken);

    Task AddAsync(CardPayment payment, CancellationToken cancellationToken);
}

/// <summary>
/// Kart ödemesi okuma tarafı. <see cref="SumActivePaymentsAsync"/> kesit ve iptal
/// semantiğini uygular: yalnızca <c>PaidOn ∈ (paidAfterExclusive, asOfDate]</c>
/// aralığındaki ve <c>asOfDate</c> kesitinde aktif ödemeler toplanır (docs/05 §5.3).
/// </summary>
public interface ICardPaymentQueries
{
    Task<PagedResult<CardPaymentDto>> ListAsync(
        Guid userId,
        Guid cardId,
        CardPaymentFilter filter,
        CancellationToken cancellationToken);

    Task<decimal> SumActivePaymentsAsync(
        Guid userId,
        Guid cardId,
        DateOnly? paidAfterExclusive,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken);
}

/// <summary>Nakit avans yazma tarafı. Bağlı para girişi aynı transaction'da yazılır (ADR-0009).</summary>
public interface ICashAdvanceRepository
{
    Task<CashAdvance?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid cashAdvanceId,
        CancellationToken cancellationToken);

    Task AddAsync(CashAdvance advance, CancellationToken cancellationToken);
}

/// <summary>
/// Nakit avans okuma tarafı. <see cref="SumActiveAdvancesAsync"/> kesit ve iptal
/// semantiğini <see cref="ICardPaymentQueries.SumActivePaymentsAsync"/> ile aynı
/// uygular: <c>TakenOn ∈ (takenAfterExclusive, asOfDate]</c> ve kesitte aktif.
/// </summary>
public interface ICashAdvanceQueries
{
    Task<PagedResult<CashAdvanceDto>> ListAsync(
        Guid userId,
        Guid cardId,
        CashAdvanceFilter filter,
        CancellationToken cancellationToken);

    Task<decimal> SumActiveAdvancesAsync(
        Guid userId,
        Guid cardId,
        DateOnly? takenAfterExclusive,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken);
}

/// <summary>Ekstre yazma tarafı. Dönem çakışması yalnızca aktif ekstrelerle kontrol edilir.</summary>
public interface IStatementRepository
{
    Task<Statement?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid statementId,
        CancellationToken cancellationToken);

    Task<bool> HasOverlappingActiveStatementAsync(
        Guid userId,
        Guid cardId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken);

    Task AddAsync(Statement statement, CancellationToken cancellationToken);
}

/// <summary>
/// Ekstre okuma tarafı. Türetilen alanlar (devreden borç, dönem sonu borcu,
/// mutabakat farkı) burada hesaplanmaz; Application katmanı okuma anında üretir.
/// </summary>
public interface IStatementQueries
{
    Task<PagedResult<StatementRecord>> ListAsync(
        Guid userId,
        Guid cardId,
        StatementFilter filter,
        CancellationToken cancellationToken);

    Task<StatementRecord?> FindAsync(
        Guid userId,
        Guid cardId,
        Guid statementId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Aynı kartta <paramref name="beforePeriodEnd"/>'den küçük en büyük
    /// <c>PeriodEnd</c>'e sahip <b>aktif</b> ekstreyi döner; yoksa <c>null</c>.
    /// </summary>
    Task<StatementRecord?> FindPreviousActiveAsync(
        Guid userId,
        Guid cardId,
        DateOnly beforePeriodEnd,
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

    /// <summary>
    /// ADR-0009: bu modülün DbContext'ini paylaşılan transaction'a bağlar.
    /// <c>null</c> çağrısı bağlantıyı temizler; scope kapandıktan sonra aynı
    /// DbContext'in yeniden kullanılabilmesi için zorunludur.
    /// </summary>
    void Enlist(IWriteTransactionScope? scope);
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
