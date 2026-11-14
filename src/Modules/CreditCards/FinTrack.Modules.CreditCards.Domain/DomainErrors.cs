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
}
