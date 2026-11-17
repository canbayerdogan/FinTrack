using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Domain;

namespace FinTrack.Modules.Debts.Application.Abstractions;

/// <summary>Kredi yazma tarafı. Taksit planı ve ödemeleri aggregate içindedir.</summary>
public interface ILoanRepository
{
    Task<Loan?> FindAsync(Guid userId, Guid loanId, CancellationToken cancellationToken);

    Task AddAsync(Loan loan, CancellationToken cancellationToken);

    /// <summary>Yeni ödemeyi açıkça <c>Added</c> olarak izler; kimliği domain üretir.</summary>
    Task AddPaymentAsync(InstallmentPayment payment, CancellationToken cancellationToken);
}

public interface ILoanQueries
{
    Task<PagedResult<LoanDto>> ListAsync(Guid userId, LoanFilter filter, CancellationToken cancellationToken);

    Task<LoanDto?> FindAsync(Guid userId, Guid loanId, CancellationToken cancellationToken);
}

/// <summary>Kişisel borç yazma tarafı. Geri ödemeler aggregate içindedir.</summary>
public interface IPersonalDebtRepository
{
    Task<PersonalDebt?> FindAsync(Guid userId, Guid debtId, CancellationToken cancellationToken);

    Task AddAsync(PersonalDebt debt, CancellationToken cancellationToken);

    /// <summary>Yeni geri ödemeyi açıkça <c>Added</c> olarak izler.</summary>
    Task AddRepaymentAsync(PersonalDebtRepayment repayment, CancellationToken cancellationToken);
}

public interface IPersonalDebtQueries
{
    Task<PagedResult<PersonalDebtDto>> ListAsync(
        Guid userId,
        PersonalDebtFilter filter,
        CancellationToken cancellationToken);

    Task<PersonalDebtDto?> FindAsync(Guid userId, Guid debtId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepaymentDto>> ListRepaymentsAsync(
        Guid userId,
        Guid debtId,
        CancellationToken cancellationToken);
}

/// <summary>Raporlama sözleşmesi için salt okunur toplamlar (Slice 8).</summary>
public interface IDebtSummaryQueries
{
    Task<decimal> GetTotalRemainingAsync(Guid userId, CancellationToken cancellationToken);
}

public enum SaveChangesOutcome
{
    Success = 0,
    ConcurrencyConflict = 1,
    DuplicateKey = 2,
}

/// <summary>
/// EF istisnalarını Application sınırında sonuçlara çevirir; handler'lar altyapı
/// tipi görmez (mimari testi bunu zorunlu kılar).
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
