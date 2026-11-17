using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Domain;

/// <summary>
/// Kullanıcının eline geçen para (docs/05 §4). Kaynak zorunludur;
/// <see cref="IsEarnedIncome"/> kaynaktan türetilir ve kullanıcı tarafından
/// değiştirilemez. Borç doğuran kaynaklar yalnızca sahibi olan modül tarafından
/// oluşturulur; bu aggregate'e bağlantı kimliği ile işaretlenir.
/// </summary>
public sealed class CashInflow : AggregateRoot
{
    public const int DescriptionMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

    /// <summary><c>decimal(19,4)</c> üst sınırı.</summary>
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    private CashInflow()
    {
    }

    public Guid UserId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Money.DefaultCurrency;

    public DateOnly OccurredOn { get; private set; }

    public CashInflowSource Source { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Borç doğuran girişlerde sahibi olan kaydın kimliği; aksi halde <c>null</c>.</summary>
    public Guid? DebtLinkId { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public CashInflowStatus Status =>
        CancelledAtUtc is null ? CashInflowStatus.Active : CashInflowStatus.Cancelled;

    /// <summary>
    /// Gelir niteliği kaynaktan türetilir (docs/05 §4.2). <see cref="CashInflowSource.Unknown"/>
    /// için belirsizdir ve gelir toplamlarına katılmaz.
    /// </summary>
    public bool? IsEarnedIncome => EarnedIncomeFor(Source);

    /// <summary>Kaynağa göre gelir niteliği; saf fonksiyon olarak da kullanılabilir.</summary>
    public static bool? EarnedIncomeFor(CashInflowSource source) => source switch
    {
        CashInflowSource.Salary or CashInflowSource.BusinessIncome => true,
        CashInflowSource.Unknown => null,
        _ => false,
    };

    /// <summary>Borç bağlantısının türü; kaynak borç doğurmuyorsa <c>null</c>.</summary>
    public DebtLinkType? DebtType => Source switch
    {
        CashInflowSource.BankLoan => DebtLinkType.Loan,
        CashInflowSource.CashAdvance => DebtLinkType.CashAdvance,
        CashInflowSource.PersonalBorrowing => DebtLinkType.PersonalDebt,
        _ => null,
    };

    public static Result<CashInflow> Create(
        Guid userId,
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        CashInflowSource source,
        string? description,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<CashInflow>(DomainErrors.UserRequired());
        }

        var validation = Validate(amount, currency, occurredOn, source, description, today, allowDebtSource: false);
        if (validation.IsFailure)
        {
            return Result.Failure<CashInflow>(validation.Error);
        }

        Normalize(currency, description, out var currencyCode, out var trimmedDescription);

        return Result.Success(new CashInflow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Currency = currencyCode,
            OccurredOn = occurredOn,
            Source = source,
            Description = trimmedDescription,
            RecordedAtUtc = nowUtc,
        });
    }

    /// <summary>
    /// Borç doğuran kaynağı sahibi olan modülün aynı kullanıcı işleminde
    /// oluşturması içindir (docs/05 §4.3). Normal <c>POST /cash-inflows</c> bu
    /// yolu kullanamaz; borç bağlantısı zorunludur.
    /// </summary>
    public static Result<CashInflow> CreateFromDebtOwner(
        Guid userId,
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        CashInflowSource source,
        Guid debtLinkId,
        string? description,
        DateOnly today,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<CashInflow>(DomainErrors.UserRequired());
        }

        if (!IsDebtSource(source))
        {
            return Result.Failure<CashInflow>(DomainErrors.CashInflowSourceInvalid());
        }

        var validation = Validate(amount, currency, occurredOn, source, description, today, allowDebtSource: true);
        if (validation.IsFailure)
        {
            return Result.Failure<CashInflow>(validation.Error);
        }

        Normalize(currency, description, out var currencyCode, out var trimmedDescription);

        return Result.Success(new CashInflow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Currency = currencyCode,
            OccurredOn = occurredOn,
            Source = source,
            DebtLinkId = debtLinkId,
            Description = trimmedDescription,
            RecordedAtUtc = nowUtc,
        });
    }

    public Result Update(
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        CashInflowSource source,
        string? description,
        DateOnly today)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Failure(DomainErrors.CashInflowCancelled());
        }

        if (DebtLinkId is not null)
        {
            return Result.Failure(DomainErrors.CashInflowDebtLinked());
        }

        var validation = Validate(amount, currency, occurredOn, source, description, today, allowDebtSource: false);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error);
        }

        Normalize(currency, description, out var currencyCode, out var trimmedDescription);

        Amount = amount;
        Currency = currencyCode;
        OccurredOn = occurredOn;
        Source = source;
        Description = trimmedDescription;
        return Result.Success();
    }

    /// <summary>İptal idempotenttir; gerekçe zorunludur (docs/05 §4.4).</summary>
    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (CancelledAtUtc is not null)
        {
            return Result.Success();
        }

        if (DebtLinkId is not null)
        {
            return Result.Failure(DomainErrors.CashInflowDebtLinked());
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

    /// <summary>
    /// Sahibi olan borç/kart modülü, bağlı para girişini kendi iptal işlemiyle
    /// birlikte iptal eder (ADR-0009, docs/05 §5.5). Normal iptal yolu borç
    /// bağlantılı kaydı reddeder; bu yol yalnızca sözleşmeyle çağrılır.
    /// </summary>
    public Result CancelFromDebtOwner(string? reason, DateTime nowUtc)
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

    /// <summary>
    /// Borç doğuran kaynağı sahibi olan kayda bağlar. Sahibi modül aynı işlemde
    /// çağırır (docs/05 §4.3); normal <c>POST /cash-inflows</c> bu yolu kullanamaz.
    /// </summary>
    public Result LinkToDebt(Guid debtLinkId)
    {
        if (DebtType is null)
        {
            return Result.Failure(DomainErrors.CashInflowSourceInvalid());
        }

        if (debtLinkId == Guid.Empty)
        {
            return Result.Failure(DomainErrors.UserRequired());
        }

        if (DebtLinkId is not null)
        {
            return Result.Failure(DomainErrors.CashInflowDebtLinked());
        }

        DebtLinkId = debtLinkId;
        return Result.Success();
    }

    private static Result Validate(
        decimal amount,
        string? currency,
        DateOnly occurredOn,
        CashInflowSource source,
        string? description,
        DateOnly today,
        bool allowDebtSource)
    {
        var amountError = DomainErrors.ValidateAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure(amountError);
        }

        if (!string.IsNullOrWhiteSpace(currency)
            && !string.Equals(currency.Trim(), Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(DomainErrors.CashInflowCurrencyUnsupported());
        }

        if (occurredOn == default)
        {
            return Result.Failure(DomainErrors.CashInflowDateRequired());
        }

        if (occurredOn > today)
        {
            return Result.Failure(DomainErrors.CashInflowDateInFuture());
        }

        if (!Enum.IsDefined(source))
        {
            return Result.Failure(DomainErrors.CashInflowSourceInvalid());
        }

        if (!allowDebtSource && IsDebtSource(source))
        {
            return Result.Failure(DomainErrors.DebtSourceRequiresOwnerEndpoint());
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null && trimmedDescription.Length > DescriptionMaxLength)
        {
            return Result.Failure(DomainErrors.CashInflowDescriptionTooLong());
        }

        return Result.Success();
    }

    private static bool IsDebtSource(CashInflowSource source) =>
        source is CashInflowSource.BankLoan
            or CashInflowSource.CashAdvance
            or CashInflowSource.PersonalBorrowing;

    private static void Normalize(
        string? currency,
        string? description,
        out string currencyCode,
        out string? trimmedDescription)
    {
        currencyCode = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
