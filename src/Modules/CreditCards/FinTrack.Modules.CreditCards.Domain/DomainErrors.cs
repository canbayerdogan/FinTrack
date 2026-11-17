using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Domain;

/// <summary>
/// Kart tanımının domain invariant hataları. Kodlar API katmanında alan/HTTP
/// eşlemesinde kullanılır; mesajlar kullanıcıya gösterilecek Türkçe metinlerdir.
/// </summary>
internal static class DomainErrors
{
    public static Error UserRequired() =>
        Error.Validation("UserRequired", "Kullanıcı kimliği zorunludur.");

    public static Error BankNameRequired() =>
        Error.Validation("CardBankNameRequired", "Banka adı zorunludur.");

    public static Error BankNameTooLong() =>
        Error.Validation(
            "CardBankNameTooLong",
            $"Banka adı en fazla {CreditCard.BankNameMaxLength} karakter olabilir.");

    public static Error DisplayNameRequired() =>
        Error.Validation("CardDisplayNameRequired", "Kart adı zorunludur.");

    public static Error DisplayNameTooLong() =>
        Error.Validation(
            "CardDisplayNameTooLong",
            $"Kart adı en fazla {CreditCard.DisplayNameMaxLength} karakter olabilir.");

    public static Error LogoKeyTooLong() =>
        Error.Validation(
            "CardLogoKeyTooLong",
            $"Logo anahtarı en fazla {CreditCard.LogoKeyMaxLength} karakter olabilir.");

    public static Error CreditLimitInvalid() =>
        Error.Validation("CardCreditLimitInvalid", "Kredi limiti 0'dan büyük olmalıdır.");

    public static Error CreditLimitScale() =>
        Error.Validation("CardCreditLimitScale", "Kredi limiti en fazla 4 ondalık basamak içerebilir.");

    public static Error CreditLimitTooLarge() =>
        Error.Validation("CardCreditLimitTooLarge", "Kredi limiti izin verilen üst sınırı aşıyor.");

    public static Error StatementDayOutOfRange() =>
        Error.Validation("CardStatementDayRange", "Ekstre günü 1 ile 31 arasında olmalıdır.");

    public static Error PaymentDueDayOutOfRange() =>
        Error.Validation("CardPaymentDueDayRange", "Son ödeme günü 1 ile 31 arasında olmalıdır.");

    public static Error CardRequired() =>
        Error.Validation("CardIdRequired", "Kart kimliği zorunludur.");

    // Kart ödemesi (docs/05 §5.4).

    public static Error PaymentAmountInvalid() =>
        Error.Validation("CardPaymentAmountInvalid", "Ödeme tutarı 0'dan büyük olmalıdır.");

    public static Error PaymentAmountScale() =>
        Error.Validation("CardPaymentAmountScale", "Ödeme tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error PaymentAmountTooLarge() =>
        Error.Validation("CardPaymentAmountTooLarge", "Ödeme tutarı izin verilen üst sınırı aşıyor.");

    public static Error PaymentCurrencyUnsupported() =>
        Error.Validation("CardPaymentCurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    public static Error PaymentDateRequired() =>
        Error.Validation("CardPaymentDateRequired", "Ödeme tarihi zorunludur.");

    public static Error PaymentDateInFuture() =>
        Error.Validation("CardPaymentDateInFuture", "Ödeme tarihi bugünden ileri olamaz.");

    public static Error PaymentNoteTooLong() =>
        Error.Validation(
            "CardPaymentNoteTooLong",
            $"Not en fazla {CardPayment.NoteMaxLength} karakter olabilir.");

    // Nakit avans (docs/05 §5.5).

    public static Error CashAdvanceAmountInvalid() =>
        Error.Validation("CashAdvanceAmountInvalid", "Avans tutarı 0'dan büyük olmalıdır.");

    public static Error CashAdvanceAmountScale() =>
        Error.Validation("CashAdvanceAmountScale", "Avans tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error CashAdvanceAmountTooLarge() =>
        Error.Validation("CashAdvanceAmountTooLarge", "Avans tutarı izin verilen üst sınırı aşıyor.");

    public static Error CashAdvanceCurrencyUnsupported() =>
        Error.Validation("CashAdvanceCurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    public static Error CashAdvanceDateRequired() =>
        Error.Validation("CashAdvanceDateRequired", "Avans tarihi zorunludur.");

    public static Error CashAdvanceDateInFuture() =>
        Error.Validation("CashAdvanceDateInFuture", "Avans tarihi bugünden ileri olamaz.");

    public static Error CashAdvanceDescriptionTooLong() =>
        Error.Validation(
            "CashAdvanceDescriptionTooLong",
            $"Açıklama en fazla {CashAdvance.DescriptionMaxLength} karakter olabilir.");

    public static Error CashAdvanceInflowRequired() =>
        Error.Validation("CashAdvanceInflowRequired", "Bağlı para girişi kimliği zorunludur.");

    public static Error CashAdvanceInflowAlreadyLinked() =>
        Error.Conflict("StateConflict", "Nakit avans zaten bir para girişine bağlı.");

    // Ekstre (docs/05 §5.6).

    public static Error StatementPeriodStartRequired() =>
        Error.Validation("StatementPeriodStartRequired", "Dönem başlangıcı zorunludur.");

    public static Error StatementPeriodEndRequired() =>
        Error.Validation("StatementPeriodEndRequired", "Dönem bitişi zorunludur.");

    public static Error StatementPeriodInvalid() =>
        Error.Validation("StatementPeriodInvalid", "Dönem başlangıcı, dönem bitişinden sonra olamaz.");

    public static Error StatementDueDateRequired() =>
        Error.Validation("StatementDueDateRequired", "Son ödeme tarihi zorunludur.");

    public static Error StatementDueDateBeforePeriodEnd() =>
        Error.Validation("StatementDueDateBeforePeriodEnd", "Son ödeme tarihi, dönem bitişinden önce olamaz.");

    public static Error StatementAmountInvalid() =>
        Error.Validation("StatementAmountInvalid", "Ekstre tutarı negatif olamaz.");

    public static Error StatementAmountScale() =>
        Error.Validation("StatementAmountScale", "Ekstre tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error StatementAmountTooLarge() =>
        Error.Validation("StatementAmountTooLarge", "Ekstre tutarı izin verilen üst sınırı aşıyor.");

    public static Error StatementCurrencyUnsupported() =>
        Error.Validation("StatementCurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    // Ortak yardımcılar.

    public static Error CancellationReasonRequired() =>
        Error.Validation("CancellationReasonRequired", "İptal gerekçesi zorunludur.");

    public static Error CancellationReasonTooLong(int maxLength) =>
        Error.Validation("CancellationReasonTooLong", $"İptal gerekçesi en fazla {maxLength} karakter olabilir.");

    public static Error? ValidateReason(string? reason, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return CancellationReasonRequired();
        }

        return reason.Trim().Length > maxLength
            ? CancellationReasonTooLong(maxLength)
            : null;
    }

    /// <summary><c>amount &gt; 0</c> ve <c>decimal(19,4)</c> sınırları.</summary>
    public static Error? ValidatePositiveAmount(
        decimal amount,
        decimal maxAmount,
        Func<Error> invalid,
        Func<Error> scale,
        Func<Error> tooLarge)
    {
        if (amount <= 0m)
        {
            return invalid();
        }

        if (decimal.Round(amount, Money.StorageScale) != amount)
        {
            return scale();
        }

        return amount > maxAmount ? tooLarge() : null;
    }

    /// <summary><c>amount &gt;= 0</c> ve <c>decimal(19,4)</c> sınırları.</summary>
    public static Error? ValidateNonNegativeAmount(
        decimal amount,
        decimal maxAmount,
        Func<Error> invalid,
        Func<Error> scale,
        Func<Error> tooLarge)
    {
        if (amount < 0m)
        {
            return invalid();
        }

        if (decimal.Round(amount, Money.StorageScale) != amount)
        {
            return scale();
        }

        return amount > maxAmount ? tooLarge() : null;
    }
}
