using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>
/// Dönemsel ekstre özeti (docs/05 §5.6). Kullanıcı <c>PeriodStart</c>,
/// <c>PeriodEnd</c>, <c>DueDate</c> ve <c>StatementAmount</c> alanlarını manuel
/// girer. <c>CarriedOverAmount</c>, <c>ComputedDebtAtPeriodEnd</c> ve
/// <c>ReconciliationDifference</c> <b>türetilir</b>; burada saklanmaz, okuma
/// anında güncel kayıtlardan hesaplanır. Ekstre yeni gider/borç kaynağı değildir.
/// </summary>
public sealed class Statement : AggregateRoot
{
    public const int CancellationReasonMaxLength = 500;

    /// <summary><c>decimal(19,4)</c> üst sınırı.</summary>
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    private Statement()
    {
    }

    public Guid UserId { get; private set; }

    public Guid CardId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public DateOnly DueDate { get; private set; }

    /// <summary>Kullanıcının girdiği ekstre tutarı; kart borcuna eklenmez.</summary>
    public decimal StatementAmount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Durum türetilir; ayrı bir durum alanı saklanmaz.</summary>
    public StatementStatus Status => CancelledAtUtc is null ? StatementStatus.Active : StatementStatus.Cancelled;

    public static Result<Statement> Create(
        Guid userId,
        Guid cardId,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        decimal statementAmount,
        string? currency,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<Statement>(DomainErrors.UserRequired());
        }

        if (cardId == Guid.Empty)
        {
            return Result.Failure<Statement>(DomainErrors.CardRequired());
        }

        if (periodStart == default)
        {
            return Result.Failure<Statement>(DomainErrors.StatementPeriodStartRequired());
        }

        if (periodEnd == default)
        {
            return Result.Failure<Statement>(DomainErrors.StatementPeriodEndRequired());
        }

        if (periodStart > periodEnd)
        {
            return Result.Failure<Statement>(DomainErrors.StatementPeriodInvalid());
        }

        if (dueDate == default)
        {
            return Result.Failure<Statement>(DomainErrors.StatementDueDateRequired());
        }

        if (dueDate < periodEnd)
        {
            return Result.Failure<Statement>(DomainErrors.StatementDueDateBeforePeriodEnd());
        }

        var amountError = DomainErrors.ValidateNonNegativeAmount(
            statementAmount,
            MaxAmount,
            DomainErrors.StatementAmountInvalid,
            DomainErrors.StatementAmountScale,
            DomainErrors.StatementAmountTooLarge);
        if (amountError is not null)
        {
            return Result.Failure<Statement>(amountError);
        }

        var currencyCode = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();
        if (!string.Equals(currencyCode, Money.DefaultCurrency, StringComparison.Ordinal))
        {
            return Result.Failure<Statement>(DomainErrors.StatementCurrencyUnsupported());
        }

        return Result.Success(new Statement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardId = cardId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            DueDate = dueDate,
            StatementAmount = statementAmount,
            Currency = currencyCode,
            RecordedAtUtc = nowUtc,
        });
    }

    /// <summary>
    /// İptal idempotenttir; gerekçe zorunludur. İptal borç hesabını değiştirmez ve
    /// dönem çakışması engelini kaldırır (docs/05 §5.6).
    /// </summary>
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
