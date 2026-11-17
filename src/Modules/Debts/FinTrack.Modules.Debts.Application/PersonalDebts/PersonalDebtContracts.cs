using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.PersonalDebts;

/// <summary>Geri ödeme yanıtı (docs/06 §7.2).</summary>
public sealed record RepaymentDto(
    Guid Id,
    Guid PersonalDebtId,
    decimal Amount,
    string Currency,
    DateOnly PaidOn,
    string? Note,
    DebtPaymentStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion);

/// <summary>Kişisel borç yanıtı (docs/06 §7.2). Kalan tutar türetilir.</summary>
public sealed record PersonalDebtDto(
    Guid Id,
    string CounterpartyName,
    decimal Principal,
    decimal RemainingAmount,
    DateOnly BorrowedOn,
    DateOnly? DueDate,
    string? Description,
    string Currency,
    PersonalDebtStatus Status,
    DateTime RecordedAtUtc,
    string RowVersion,
    IReadOnlyList<RepaymentDto> Repayments);

public sealed record PersonalDebtFilter(
    PersonalDebtStatus? Status,
    bool IncludeCancelled,
    int Page,
    int PageSize);

public sealed record ListPersonalDebtsQuery(PersonalDebtFilter Filter);

public sealed record GetPersonalDebtQuery(Guid DebtId);

public sealed record CreatePersonalDebtCommand(
    string? CounterpartyName,
    decimal Principal,
    DateOnly BorrowedOn,
    DateOnly? DueDate,
    string? Description,
    string? Currency);

public sealed record CancelPersonalDebtCommand(Guid DebtId, string? Reason, string? RowVersion);

public sealed record AddRepaymentCommand(
    Guid DebtId,
    decimal Amount,
    DateOnly PaidOn,
    string? Note);

public sealed record CancelRepaymentCommand(
    Guid DebtId,
    Guid RepaymentId,
    string? Reason,
    string? RowVersion);

public sealed record ListRepaymentsQuery(Guid DebtId);

public static class PersonalDebtMapper
{
    public static PersonalDebtDto ToDto(PersonalDebt debt) => new(
        debt.Id,
        debt.CounterpartyName,
        debt.Principal,
        debt.RemainingAmount,
        debt.BorrowedOn,
        debt.DueDate,
        debt.Description,
        debt.Currency,
        debt.Status,
        debt.RecordedAtUtc,
        RowVersionCodec.ToBase64(debt.RowVersion),
        debt.Repayments
            .Where(repayment => repayment.Status == DebtPaymentStatus.Active)
            .OrderByDescending(repayment => repayment.PaidOn)
            .ThenByDescending(repayment => repayment.RecordedAtUtc)
            .Select(ToRepaymentDto)
            .ToList());

    public static RepaymentDto ToRepaymentDto(PersonalDebtRepayment repayment) => new(
        repayment.Id,
        repayment.PersonalDebtId,
        repayment.Amount,
        repayment.Currency,
        repayment.PaidOn,
        repayment.Note,
        repayment.Status,
        repayment.RecordedAtUtc,
        RowVersionCodec.ToBase64(repayment.RowVersion));
}
