using System.Data.Common;
using FinTrack.SharedKernel;

namespace FinTrack.ModuleContracts;

/// <summary>
/// Aynı istekte yazma yapan modüllerin kullandığı tek fiziksel bağlantı (ADR-0009).
/// Yaşam döngüsü host/DI kapsamındadır; modüller bağlantıyı kapatmaz.
/// </summary>
public interface IWriteConnectionProvider
{
    DbConnection Connection { get; }
}

/// <summary>
/// Modüller arası açık transaction sınırı (ADR-0009). EF tipleri sözleşmeye sızmaz;
/// her modül kendi DbContext'ini <see cref="Transaction"/> ile ilişkilendirir.
/// </summary>
public interface IWriteTransactionScope : IAsyncDisposable
{
    DbTransaction Transaction { get; }

    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IWriteTransactionScopeFactory
{
    Task<IWriteTransactionScope> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>Borç bağlantısının türü; modül iç enum'lardan bağımsız sözleşme değeri.</summary>
public enum DebtInflowKind
{
    CashAdvance = 0,
    Loan = 1,
    PersonalDebt = 2,
}

/// <summary>Borç sahibi modülün açtığı transaction'da oluşturulacak para girişi.</summary>
public sealed record DebtInflowCreate(
    Guid UserId,
    decimal Amount,
    string? Currency,
    DateOnly OccurredOn,
    DebtInflowKind Kind,
    Guid DebtLinkId,
    string? Description,
    DateOnly Today,
    DateTime NowUtc);

/// <summary>
/// Bağlı para girişinin iptali. <see cref="Kind"/> ve <see cref="DebtLinkId"/>,
/// çağıranın yalnızca kendi borç kaydına bağlı girişi iptal edebilmesini sağlar.
/// </summary>
public sealed record DebtInflowCancel(
    Guid UserId,
    Guid CashInflowId,
    DebtInflowKind Kind,
    Guid DebtLinkId,
    string Reason,
    DateTime NowUtc);

/// <summary>
/// Borç sahibi modüllerin `inflows` şemasına yazma sözleşmesi (ADR-0009).
/// Uygulaması CashInflows modülündedir; çağıran modül transaction'ı açar ve
/// sözleşmeye <see cref="IWriteTransactionScope"/> olarak geçirir.
/// </summary>
public interface IDebtCashInflowWriter
{
    /// <summary>Parametreleri doğrular, para girişini ekler ve kendi DbContext'ini kaydeder.</summary>
    Task<Result<Guid>> CreateAsync(
        DebtInflowCreate request,
        IWriteTransactionScope scope,
        CancellationToken cancellationToken);

    /// <summary>Bağlı para girişini iptal eder (idempotent); gerekçe zorunludur.</summary>
    Task<Result> CancelAsync(
        DebtInflowCancel request,
        IWriteTransactionScope scope,
        CancellationToken cancellationToken);
}
