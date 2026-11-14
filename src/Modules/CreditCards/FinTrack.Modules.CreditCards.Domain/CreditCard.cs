using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>
/// Kullanıcıya ait kredi kartı tanımı. Gerçek kart numarası, CVV, son kullanma
/// tarihi veya banka şifresi tutmaz (docs/05 §5.1). Kart silinmez; arşivlenir.
/// Borç bu aggregate'te saklanmaz; harcama/iadeden hesaplanır (ADR-0008).
/// </summary>
public sealed class CreditCard : AggregateRoot
{
    public const int BankNameMaxLength = 100;
    public const int DisplayNameMaxLength = 100;
    public const int LogoKeyMaxLength = 50;

    /// <summary><c>decimal(19,4)</c> üst sınırı.</summary>
    public const decimal MaxCreditLimit = 999_999_999_999_999.9999m;

    private CreditCard()
    {
    }

    public Guid UserId { get; private set; }

    public string BankName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? LogoKey { get; private set; }

    /// <summary>Bilgi amaçlıdır; borç hesabına girmez (docs/05 §5.1).</summary>
    public decimal? CreditLimit { get; private set; }

    public int? StatementDay { get; private set; }

    public int? PaymentDueDay { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Durum türetilir; ayrı bir durum alanı saklanmaz.</summary>
    public CardStatus Status => IsArchived ? CardStatus.Archived : CardStatus.Active;

    public static Result<CreditCard> Create(
        Guid userId,
        string? bankName,
        string? displayName,
        string? logoKey,
        decimal? creditLimit,
        int? statementDay,
        int? paymentDueDay,
        DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<CreditCard>(DomainErrors.UserRequired());
        }

        var validation = Validate(
            bankName,
            displayName,
            logoKey,
            creditLimit,
            statementDay,
            paymentDueDay,
            out var trimmedBankName,
            out var trimmedDisplayName,
            out var trimmedLogoKey);
        if (validation.IsFailure)
        {
            return Result.Failure<CreditCard>(validation.Error);
        }

        return Result.Success(new CreditCard
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankName = trimmedBankName,
            DisplayName = trimmedDisplayName,
            LogoKey = trimmedLogoKey,
            CreditLimit = creditLimit,
            StatementDay = statementDay,
            PaymentDueDay = paymentDueDay,
            CreatedAtUtc = nowUtc,
        });
    }

    /// <summary>
    /// Kart tanımını günceller. Arşivli kartın meta verisi de düzeltilebilir;
    /// arşivden geri açma yoktur (docs/05 §5.1).
    /// </summary>
    public Result Update(
        string? bankName,
        string? displayName,
        string? logoKey,
        decimal? creditLimit,
        int? statementDay,
        int? paymentDueDay)
    {
        var validation = Validate(
            bankName,
            displayName,
            logoKey,
            creditLimit,
            statementDay,
            paymentDueDay,
            out var trimmedBankName,
            out var trimmedDisplayName,
            out var trimmedLogoKey);
        if (validation.IsFailure)
        {
            return validation;
        }

        BankName = trimmedBankName;
        DisplayName = trimmedDisplayName;
        LogoKey = trimmedLogoKey;
        CreditLimit = creditLimit;
        StatementDay = statementDay;
        PaymentDueDay = paymentDueDay;
        return Result.Success();
    }

    /// <summary>Arşivleme idempotenttir; ikinci çağrı hata üretmez.</summary>
    public Result Archive(DateTime nowUtc)
    {
        if (IsArchived)
        {
            return Result.Success();
        }

        IsArchived = true;
        ArchivedAtUtc = nowUtc;
        return Result.Success();
    }

    private static Result Validate(
        string? bankName,
        string? displayName,
        string? logoKey,
        decimal? creditLimit,
        int? statementDay,
        int? paymentDueDay,
        out string trimmedBankName,
        out string trimmedDisplayName,
        out string? trimmedLogoKey)
    {
        trimmedBankName = bankName?.Trim() ?? string.Empty;
        trimmedDisplayName = displayName?.Trim() ?? string.Empty;
        trimmedLogoKey = string.IsNullOrWhiteSpace(logoKey) ? null : logoKey.Trim();

        if (trimmedBankName.Length == 0)
        {
            return Result.Failure(DomainErrors.BankNameRequired());
        }

        if (trimmedBankName.Length > BankNameMaxLength)
        {
            return Result.Failure(DomainErrors.BankNameTooLong());
        }

        if (trimmedDisplayName.Length == 0)
        {
            return Result.Failure(DomainErrors.DisplayNameRequired());
        }

        if (trimmedDisplayName.Length > DisplayNameMaxLength)
        {
            return Result.Failure(DomainErrors.DisplayNameTooLong());
        }

        if (trimmedLogoKey is not null && trimmedLogoKey.Length > LogoKeyMaxLength)
        {
            return Result.Failure(DomainErrors.LogoKeyTooLong());
        }

        if (creditLimit is not null)
        {
            if (creditLimit <= 0m)
            {
                return Result.Failure(DomainErrors.CreditLimitInvalid());
            }

            if (decimal.Round(creditLimit.Value, Money.StorageScale) != creditLimit.Value)
            {
                return Result.Failure(DomainErrors.CreditLimitScale());
            }

            if (creditLimit > MaxCreditLimit)
            {
                return Result.Failure(DomainErrors.CreditLimitTooLarge());
            }
        }

        if (statementDay is int statementDayValue && (statementDayValue < 1 || statementDayValue > 31))
        {
            return Result.Failure(DomainErrors.StatementDayOutOfRange());
        }

        if (paymentDueDay is int paymentDueDayValue && (paymentDueDayValue < 1 || paymentDueDayValue > 31))
        {
            return Result.Failure(DomainErrors.PaymentDueDayOutOfRange());
        }

        return Result.Success();
    }
}
