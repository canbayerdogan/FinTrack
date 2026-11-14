using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Domain;

/// <summary>
/// Tek bir harcama. Kredi kartı alışverişi de bir harcamadır; ayrı kart işlemi
/// kaydı tutulmaz (ADR-0008). Harcama durumu saklanmaz, aktif iadelerden türetilir.
/// </summary>
public sealed class Expense : AggregateRoot
{
    public const int DescriptionMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    /// <summary><c>decimal(19,4)</c> üst sınırı.</summary>
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    private readonly List<Refund> _refunds = [];

    private Expense()
    {
    }

    public Guid UserId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateOnly OccurredOn { get; private set; }

    public Guid CategoryId { get; private set; }

    public string? Description { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Yalnızca <see cref="PaymentMethod.CreditCard"/> iken dolu; kartın sahibi CreditCards modülüdür.</summary>
    public Guid? CardId { get; private set; }

    /// <summary>Oluşturulma anı; sonradan değiştirilemez.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<Refund> Refunds => _refunds;

    /// <summary>Aktif iadelerin toplamı; iptal edilmiş iadeler sayılmaz.</summary>
    public decimal RefundedAmount => _refunds
        .Where(refund => refund.Status == RefundStatus.Active)
        .Sum(refund => refund.Amount);

    public decimal NetAmount => Amount - RefundedAmount;

    /// <summary>
    /// Durum türetilir: iptal → <see cref="ExpenseStatus.Cancelled"/>;
    /// iade yok → Active; kısmi → PartiallyRefunded; tamamı → Refunded.
    /// </summary>
    public ExpenseStatus Status
    {
        get
        {
            if (CancelledAtUtc is not null)
            {
                return ExpenseStatus.Cancelled;
            }

            var refundedAmount = RefundedAmount;
            if (refundedAmount <= 0m)
            {
                return ExpenseStatus.Active;
            }

            return refundedAmount >= Amount
                ? ExpenseStatus.Refunded
                : ExpenseStatus.PartiallyRefunded;
        }
    }

    public static Result<Expense> Create(
        Guid userId,
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        Guid categoryId,
        string? description,
        PaymentMethod paymentMethod,
        Guid? cardId,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<Expense>(DomainErrors.UserRequired());
        }

        var validation = Validate(
            amount,
            currency,
            occurredOn,
            categoryId,
            description,
            paymentMethod,
            cardId,
            today);
        if (validation.IsFailure)
        {
            return Result.Failure<Expense>(validation.Error);
        }

        NormalizeAmountCurrency(currency, out var currencyCode);
        NormalizeDescription(description, out var trimmedDescription);
        NormalizeCard(paymentMethod, cardId, out var normalizedCardId);

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Currency = currencyCode,
            OccurredOn = occurredOn,
            CategoryId = categoryId,
            Description = trimmedDescription,
            PaymentMethod = paymentMethod,
            CardId = normalizedCardId,
            RecordedAtUtc = nowUtc,
        };

        return Result.Success(expense);
    }

    public Result Update(
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        Guid categoryId,
        string? description,
        PaymentMethod paymentMethod,
        Guid? cardId,
        DateOnly today)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Failure(DomainErrors.ExpenseCancelled());
        }

        var validation = Validate(
            amount,
            currency,
            occurredOn,
            categoryId,
            description,
            paymentMethod,
            cardId,
            today);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error);
        }

        if (amount < RefundedAmount)
        {
            return Result.Failure(DomainErrors.ExpenseAmountBelowRefunds());
        }

        var earliestActiveRefund = _refunds
            .Where(refund => refund.Status == RefundStatus.Active)
            .Select(refund => (DateOnly?)refund.RefundedOn)
            .Min();

        if (earliestActiveRefund is not null && occurredOn > earliestActiveRefund.Value)
        {
            return Result.Failure(DomainErrors.ExpenseDateBeforeRefunds());
        }

        NormalizeAmountCurrency(currency, out var currencyCode);
        NormalizeDescription(description, out var trimmedDescription);
        NormalizeCard(paymentMethod, cardId, out var normalizedCardId);

        Amount = amount;
        Currency = currencyCode;
        OccurredOn = occurredOn;
        CategoryId = categoryId;
        Description = trimmedDescription;
        PaymentMethod = paymentMethod;
        CardId = normalizedCardId;
        return Result.Success();
    }

    /// <summary>İptal idempotenttir; ikinci çağrı hata üretmez.</summary>
    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Success();
        }

        if (_refunds.Any(refund => refund.Status == RefundStatus.Active))
        {
            return Result.Failure(DomainErrors.ExpenseHasActiveRefunds());
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

    public Result<Refund> AddRefund(
        decimal amount,
        string? currency,
        DateOnly refundedOn,
        string? description,
        DateOnly today,
        DateTime nowUtc)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Failure<Refund>(DomainErrors.ExpenseCancelled("İptal edilmiş harcamaya iade eklenemez."));
        }

        if (refundedOn < OccurredOn)
        {
            return Result.Failure<Refund>(DomainErrors.RefundDateBeforeExpense());
        }

        var refundResult = Refund.Create(
            Id,
            UserId,
            amount,
            currency,
            refundedOn,
            description,
            today,
            nowUtc);
        if (refundResult.IsFailure)
        {
            return refundResult;
        }

        if (RefundedAmount + refundResult.Value.Amount > Amount)
        {
            return Result.Failure<Refund>(DomainErrors.RefundExceedsExpense());
        }

        _refunds.Add(refundResult.Value);
        return refundResult;
    }

    /// <summary>İade iptali idempotenttir; zaten iptal edilmiş iade hata üretmez.</summary>
    public Result CancelRefund(Guid refundId, string? reason, DateTime nowUtc)
    {
        var refund = _refunds.FirstOrDefault(candidate => candidate.Id == refundId);
        if (refund is null)
        {
            return Result.Failure(DomainErrors.RefundNotFound());
        }

        return refund.Cancel(reason, nowUtc);
    }

    private static Result Validate(
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        Guid categoryId,
        string? description,
        PaymentMethod paymentMethod,
        Guid? cardId,
        DateOnly today)
    {
        var amountError = DomainErrors.ValidateAmount(
            amount,
            DomainErrors.ExpenseAmountInvalid,
            DomainErrors.ExpenseAmountScale,
            DomainErrors.ExpenseAmountTooLarge);
        if (amountError is not null)
        {
            return Result.Failure(amountError);
        }

        if (!string.IsNullOrWhiteSpace(currency)
            && !string.Equals(currency.Trim(), Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(DomainErrors.ExpenseCurrencyUnsupported());
        }

        if (occurredOn == default)
        {
            return Result.Failure(DomainErrors.ExpenseDateRequired());
        }

        if (occurredOn > today)
        {
            return Result.Failure(DomainErrors.ExpenseDateInFuture());
        }

        if (categoryId == Guid.Empty)
        {
            return Result.Failure(DomainErrors.ExpenseCategoryRequired());
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null && trimmedDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure(DomainErrors.ExpenseDescriptionTooLong());
        }

        if (!Enum.IsDefined(paymentMethod))
        {
            return Result.Failure(DomainErrors.ExpensePaymentMethodInvalid());
        }

        if (paymentMethod == PaymentMethod.CreditCard && (cardId is null || cardId == Guid.Empty))
        {
            return Result.Failure(DomainErrors.ExpenseCardRequired());
        }

        if (paymentMethod != PaymentMethod.CreditCard && cardId is not null)
        {
            return Result.Failure(DomainErrors.ExpenseCardNotAllowed());
        }

        return Result.Success();
    }

    private static void NormalizeAmountCurrency(string? currency, out string currencyCode) =>
        currencyCode = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();

    private static void NormalizeDescription(string? description, out string? trimmedDescription) =>
        trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static void NormalizeCard(PaymentMethod paymentMethod, Guid? cardId, out Guid? normalizedCardId) =>
        normalizedCardId = paymentMethod == PaymentMethod.CreditCard ? cardId : null;
}
