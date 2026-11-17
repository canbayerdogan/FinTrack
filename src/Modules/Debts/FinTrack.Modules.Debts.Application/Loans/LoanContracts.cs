using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.Loans;

/// <summary>Taksit ödemesi (docs/06 §7.1). İptal edilmiş ödeme DTO'da dönmez.</summary>
public sealed record InstallmentPaymentDto(
    Guid Id,
    decimal Amount,
    DateOnly PaidOn,
    DebtPaymentStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion);

public sealed record InstallmentDto(
    int Number,
    DateOnly DueDate,
    decimal Amount,
    InstallmentStatus Status,
    DateOnly? PaidOn,
    InstallmentPaymentDto? Payment);

/// <summary>
/// Kredi yanıtı (docs/06 §7.1). <c>totalRepayment</c> türetilir; ayrı
/// <c>installmentAmount</c> alanı yoktur.
/// </summary>
public sealed record LoanDto(
    Guid Id,
    decimal Principal,
    decimal MonthlyPayment,
    int InstallmentCount,
    decimal TotalRepayment,
    DateOnly FirstDueDate,
    string? BankName,
    string? Description,
    string Currency,
    LoanStatus Status,
    decimal RemainingAmount,
    DateTime RecordedAtUtc,
    string RowVersion,
    IReadOnlyList<InstallmentDto> Installments);

public sealed record LoanFilter(
    LoanStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListLoansQuery(LoanFilter Filter);

public sealed record GetLoanQuery(Guid LoanId);

public sealed record CreateLoanCommand(
    decimal Principal,
    decimal MonthlyPayment,
    int InstallmentCount,
    DateOnly FirstDueDate,
    string? BankName,
    string? Description,
    string? Currency);

public sealed record CancelLoanCommand(Guid LoanId, string? Reason, string? RowVersion);

public sealed record PayInstallmentCommand(
    Guid LoanId,
    int Number,
    DateOnly PaidOn,
    string? RowVersion);

public sealed record CancelInstallmentPaymentCommand(
    Guid LoanId,
    int Number,
    Guid PaymentId,
    string? Reason,
    string? RowVersion);

public static class LoanMapper
{
    public static LoanDto ToDto(Loan loan) => new(
        loan.Id,
        loan.Principal,
        loan.MonthlyPayment,
        loan.InstallmentCount,
        loan.TotalRepayment,
        loan.FirstDueDate,
        loan.BankName,
        loan.Description,
        loan.Currency,
        loan.Status,
        loan.RemainingAmount,
        loan.RecordedAtUtc,
        RowVersionCodec.ToBase64(loan.RowVersion),
        loan.Installments
            .OrderBy(installment => installment.Number)
            .Select(ToInstallmentDto)
            .ToList());

    private static InstallmentDto ToInstallmentDto(Installment installment)
    {
        var payment = installment.ActivePayment;
        return new InstallmentDto(
            installment.Number,
            installment.DueDate,
            installment.Amount,
            installment.Status,
            payment?.PaidOn,
            payment is null ? null : ToPaymentDto(payment));
    }

    private static InstallmentPaymentDto ToPaymentDto(InstallmentPayment payment) => new(
        payment.Id,
        payment.Amount,
        payment.PaidOn,
        payment.Status,
        payment.RecordedAtUtc,
        RowVersionCodec.ToBase64(payment.RowVersion));
}
