using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Domain;

/// <summary>
/// Bir taksite ait gerçek ödeme kaydı. İptal edilen ödeme silinmez; aynı taksit
/// yeniden ödenebilir (2026-11-17 kararı).
/// </summary>
public sealed class InstallmentPayment : Entity
{
    public const int CancellationReasonMaxLength = 500;

    private InstallmentPayment()
    {
    }

    public Guid UserId { get; private set; }

    public Guid InstallmentId { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly PaidOn { get; private set; }

    public DebtPaymentStatus Status { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal static InstallmentPayment Start(
        Guid userId,
        Guid installmentId,
        decimal amount,
        DateOnly paidOn,
        DateTime nowUtc) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallmentId = installmentId,
            Amount = amount,
            PaidOn = paidOn,
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
