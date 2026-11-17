using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>
/// Karta bağlı nakit avans (docs/05 §5.5). Gider değildir; kart borcunun
/// bileşenidir ve aynı kullanıcı işleminde bağlı bir <c>CashInflow</c> oluşturur
/// (ADR-0009). Geri ödeme için ayrı kayıt yoktur; kart ödemesi kullanılır.
/// </summary>
public sealed class CashAdvance : AggregateRoot
{
    public const int DescriptionMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    /// <summary><c>decimal(19,4)</c> üst sınırı.</summary>
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    private CashAdvance()
    {
    }

    public Guid UserId { get; private set; }

    public Guid CardId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    /// <summary>Finansal gerçekleşme tarihi; kesit hesabında esas alınır.</summary>
    public DateOnly TakenOn { get; private set; }

    public string? Description { get; private set; }

    /// <summary>ADR-0009 ile aynı işlemde oluşturulan para girişinin kimliği.</summary>
    public Guid? LinkedCashInflowId { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Durum türetilir; ayrı durum kolonu saklanmaz.</summary>
    public CashAdvanceStatus Status => CancelledAtUtc is null
        ? CashAdvanceStatus.Active
        : CashAdvanceStatus.Cancelled;

    public static Result<CashAdvance> Create(
        Guid userId,
        Guid cardId,
        decimal amount,
        string? currency,
        DateOnly takenOn,
        string? description,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<CashAdvance>(DomainErrors.UserRequired());
        }

        if (cardId == Guid.Empty)
        {
            return Result.Failure<CashAdvance>(DomainErrors.CardRequired());
        }

        var amountError = DomainErrors.ValidatePositiveAmount(
            amount,
            MaxAmount,
            DomainErrors.CashAdvanceAmountInvalid,
            DomainErrors.CashAdvanceAmountScale,
            DomainErrors.CashAdvanceAmountTooLarge);
        if (amountError is not null)
        {
            return Result.Failure<CashAdvance>(amountError);
        }

        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();
        if (!string.Equals(currencyCode, Money.DefaultCurrency, StringComparison.Ordinal))
        {
            return Result.Failure<CashAdvance>(DomainErrors.CashAdvanceCurrencyUnsupported());
        }

        if (takenOn == default)
        {
            return Result.Failure<CashAdvance>(DomainErrors.CashAdvanceDateRequired());
        }

        if (takenOn > today)
        {
            return Result.Failure<CashAdvance>(DomainErrors.CashAdvanceDateInFuture());
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null && trimmedDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure<CashAdvance>(DomainErrors.CashAdvanceDescriptionTooLong());
        }

        return Result.Success(new CashAdvance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardId = cardId,
            Amount = amount,
            Currency = currencyCode,
            TakenOn = takenOn,
            Description = trimmedDescription,
            RecordedAtUtc = nowUtc,
        });
    }

    /// <summary>Writer para girişini aynı transaction'da oluşturduktan sonra bağlanır.</summary>
    public Result LinkCashInflow(Guid cashInflowId)
    {
        if (cashInflowId == Guid.Empty)
        {
            return Result.Failure(DomainErrors.CashAdvanceInflowRequired());
        }

        if (LinkedCashInflowId is not null)
        {
            return Result.Failure(DomainErrors.CashAdvanceInflowAlreadyLinked());
        }

        LinkedCashInflowId = cashInflowId;
        return Result.Success();
    }

    /// <summary>İptal idempotenttir; gerekçe zorunludur (docs/05 §5.5).</summary>
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
