using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Domain;

/// <summary>
/// Harcamaya bağlı iade. Harcamayı silmez; net tutarı azaltır ve gelir oluşturmaz.
/// </summary>
public sealed class Refund : Entity
{
    public const int DescriptionMaxLength = 500;

    private Refund()
    {
    }

    public Guid ExpenseId { get; private set; }

    public Guid UserId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateOnly RefundedOn { get; private set; }

    public string? Description { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public RefundStatus Status => CancelledAtUtc is null ? RefundStatus.Active : RefundStatus.Cancelled;

    internal static Result<Refund> Create(
        Guid expenseId,
        Guid userId,
        decimal amount,
        string? currency,
        DateOnly refundedOn,
        string? description,
        DateOnly today,
        DateTime nowUtc)
    {
        var amountError = DomainErrors.ValidateAmount(
            amount,
            DomainErrors.RefundAmountInvalid,
            DomainErrors.RefundAmountScale,
            DomainErrors.RefundAmountTooLarge);
        if (amountError is not null)
        {
            return Result.Failure<Refund>(amountError);
        }

        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        if (!string.Equals(currencyCode, Money.DefaultCurrency, StringComparison.Ordinal))
        {
            return Result.Failure<Refund>(DomainErrors.ExpenseCurrencyUnsupported());
        }

        if (refundedOn == default)
        {
            return Result.Failure<Refund>(DomainErrors.RefundDateRequired());
        }

        if (refundedOn > today)
        {
            return Result.Failure<Refund>(DomainErrors.RefundDateInFuture());
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null && trimmedDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure<Refund>(DomainErrors.RefundDescriptionTooLong());
        }

        return Result.Success(new Refund
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            UserId = userId,
            Amount = amount,
            Currency = currencyCode,
            RefundedOn = refundedOn,
            Description = trimmedDescription,
            RecordedAtUtc = nowUtc,
        });
    }

    /// <summary>İade iptali idempotenttir; zaten iptal edilmişse hata üretmez.</summary>
    internal Result Cancel(string? reason, DateTime nowUtc)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Success();
        }

        var reasonError = DomainErrors.ValidateReason(reason);
        if (reasonError is not null)
        {
            return Result.Failure(reasonError);
        }

        CancelledAtUtc = nowUtc;
        CancellationReason = reason!.Trim();
        return Result.Success();
    }
}
