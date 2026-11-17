using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Domain;

/// <summary>Kişisel borca yapılan geri ödeme (docs/05 §6.6). Gider değildir.</summary>
public sealed class PersonalDebtRepayment : Entity
{
    public const int NoteMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    private PersonalDebtRepayment()
    {
    }

    public Guid UserId { get; private set; }

    public Guid PersonalDebtId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateOnly PaidOn { get; private set; }

    public string? Note { get; private set; }

    public DebtPaymentStatus Status { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal static PersonalDebtRepayment Start(
        Guid userId,
        Guid personalDebtId,
        decimal amount,
        string currency,
        DateOnly paidOn,
        string? note,
        DateTime nowUtc) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PersonalDebtId = personalDebtId,
            Amount = amount,
            Currency = currency,
            PaidOn = paidOn,
            Note = note,
            Status = DebtPaymentStatus.Active,
            RecordedAtUtc = nowUtc,
        };

    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (Status == DebtPaymentStatus.Cancelled)
        {
            return Result.Success();
        }

        var reasonError = DomainErrors.ValidateReason(reason);
        if (reasonError is not null)
        {
            return Result.Failure(reasonError);
        }

        Status = DebtPaymentStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        CancellationReason = reason!.Trim();
        return Result.Success();
    }
}
