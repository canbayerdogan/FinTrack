using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>
/// Karta yapılan ödeme (docs/05 §5.4). Ödeme <b>gider değildir</b>; kart borcunu
/// azaltır ve borcu aşabilir. Kayıt silinmez; iptal edilir. İptal zamanı
/// (<c>CancelledAtUtc</c>) sunucuda UTC tutulan ayrı bir sistem zamanıdır;
/// <c>PaidOn</c> finansal gerçekleşme tarihidir ve iptal onu değiştirmez.
/// </summary>
public sealed class CardPayment : AggregateRoot
{
    public const int NoteMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    /// <summary><c>decimal(19,4)</c> üst sınırı.</summary>
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    private CardPayment()
    {
    }

    public Guid UserId { get; private set; }

    public Guid CardId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    /// <summary>Finansal gerçekleşme tarihi; kesit ve devreden borç hesabında esas alınır.</summary>
    public DateOnly PaidOn { get; private set; }

    public string? Note { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Durum türetilir; ayrı bir durum alanı saklanmaz.</summary>
    public PaymentStatus Status => CancelledAtUtc is null ? PaymentStatus.Active : PaymentStatus.Cancelled;

    public static Result<CardPayment> Create(
        Guid userId,
        Guid cardId,
        decimal amount,
        string? currency,
        DateOnly paidOn,
        string? note,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<CardPayment>(DomainErrors.UserRequired());
        }

        if (cardId == Guid.Empty)
        {
            return Result.Failure<CardPayment>(DomainErrors.CardRequired());
        }

        var amountError = DomainErrors.ValidatePositiveAmount(
            amount,
            MaxAmount,
            DomainErrors.PaymentAmountInvalid,
            DomainErrors.PaymentAmountScale,
            DomainErrors.PaymentAmountTooLarge);
        if (amountError is not null)
        {
            return Result.Failure<CardPayment>(amountError);
        }

        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();
        if (!string.Equals(currencyCode, Money.DefaultCurrency, StringComparison.Ordinal))
        {
            return Result.Failure<CardPayment>(DomainErrors.PaymentCurrencyUnsupported());
        }

        if (paidOn == default)
        {
            return Result.Failure<CardPayment>(DomainErrors.PaymentDateRequired());
        }

        if (paidOn > today)
        {
            return Result.Failure<CardPayment>(DomainErrors.PaymentDateInFuture());
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is not null && trimmedNote.Length > NoteMaxLength)
        {
            return Result.Failure<CardPayment>(DomainErrors.PaymentNoteTooLong());
        }

        return Result.Success(new CardPayment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardId = cardId,
            Amount = amount,
            Currency = currencyCode,
            PaidOn = paidOn,
            Note = trimmedNote,
            RecordedAtUtc = nowUtc,
        });
    }

    /// <summary>İptal idempotenttir; gerekçe zorunludur (docs/05 §5.4).</summary>
    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Success();
        }

        var reasonError = DomainErrors.ValidateReason(reason, CancellationReasonMaxLength);
        if (reasonError is not null)
        {
            return Result.Failure(reasonError);
        }

        CancelledAtUtc = nowUtc;
        CancellationReason = reason!.Trim();
        return Result.Success();
    }
}
