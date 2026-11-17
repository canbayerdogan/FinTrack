namespace FinTrack.ModuleContracts;

/// <summary>
/// Raporlama için modül sahiplerinin sunduğu salt okunur projeksiyonlar.
/// </summary>
/// <remarks>
/// Uygulamalar verinin sahibi olduğu modülün <c>Application</c> katmanındadır.
/// Reporting yalnızca bu sözleşmeleri görür; başka modülün DbContext'ine veya
/// tablosuna erişmez (docs/03 §6, docs/05 §7.6).
/// </remarks>

public sealed record ExpenseCategoryTotal(
    Guid CategoryId,
    string CategoryName,
    decimal NetAmount);

public sealed record ExpenseReportTotals(
    decimal Gross,
    decimal Refunds,
    decimal Net,
    IReadOnlyList<ExpenseCategoryTotal> Categories);

/// <summary>
/// Aylık harcama/iade özeti. İade, orijinal harcamanın kategorisinde
/// <c>RefundedOn</c> tarihinin ait olduğu dönemde negatif gider olarak yansır.
/// </summary>
public interface IExpenseReportQuery
{
    Task<ExpenseReportTotals> GetMonthlyAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed record CashInflowSourceTotal(string Source, decimal Amount);

public sealed record CashInflowReportTotals(
    decimal EarnedIncome,
    decimal OtherInflowsTotal,
    IReadOnlyList<CashInflowSourceTotal> OtherInflows,
    decimal UnknownInflowsTotal,
    int UnknownInflowsCount);

/// <summary>
/// Aktif para girişlerinin dönem özeti: kazanılmış gelir, bilinen diğer
/// kaynaklar ve ayrı <c>Unknown</c> grubu.
/// </summary>
public interface ICashInflowReportQuery
{
    Task<CashInflowReportTotals> GetMonthlyAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed record CardDebtEntry(
    Guid CardId,
    string DisplayName,
    decimal Debt,
    bool IsInCredit);

public sealed record CardDebtReport(
    IReadOnlyList<CardDebtEntry> Cards,
    decimal CardCreditTotal);

public sealed record CardStatementDue(
    Guid CardId,
    Guid StatementId,
    DateOnly DueDate,
    decimal Amount);

/// <summary>
/// Kart borçları ve aktif ekstre son ödeme tarihleri. Ekstre tutarı borca
/// eklenmez; negatif kart bakiyesi ayrı raporlanır.
/// </summary>
public interface ICardReportQuery
{
    Task<CardDebtReport> GetDebtAsync(
        Guid userId,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CardStatementDue>> GetStatementDuesAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed record LoanDebtEntry(Guid LoanId, string? BankName, decimal RemainingAmount);

public sealed record PersonalDebtEntry(
    Guid PersonalDebtId,
    string CounterpartyName,
    decimal RemainingAmount);

public sealed record LoanInstallmentDue(
    Guid LoanId,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount);

/// <summary>
/// Kredi / kişisel borç kalan tutarları ve planlanmış taksit vadeleri.
/// </summary>
public interface IDebtReportQuery
{
    Task<IReadOnlyList<LoanDebtEntry>> GetLoansAsync(
        Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalDebtEntry>> GetPersonalDebtsAsync(
        Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoanInstallmentDue>> GetUpcomingInstallmentsAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
