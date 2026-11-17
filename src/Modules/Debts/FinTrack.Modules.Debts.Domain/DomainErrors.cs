using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Domain;

/// <summary>
/// Borç aggregate'lerinin invariant hataları. Kodlar API katmanında alan/HTTP
/// eşlemesinde kullanılır; mesajlar kullanıcıya gösterilecek Türkçe metinlerdir.
/// </summary>
internal static class DomainErrors
{
    public const int MaxAmountScale = Money.StorageScale;
    public const decimal MaxAmount = 999_999_999_999_999.9999m;

    public static Error UserRequired() =>
        Error.Validation("UserRequired", "Kullanıcı kimliği zorunludur.");

    public static Error LoanPrincipalInvalid() =>
        Error.Validation("LoanPrincipalInvalid", "Kredi tutarı 0'dan büyük olmalıdır.");

    public static Error LoanPrincipalScale() =>
        Error.Validation("LoanPrincipalScale", "Kredi tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error LoanPrincipalTooLarge() =>
        Error.Validation("LoanPrincipalTooLarge", "Kredi tutarı izin verilen üst sınırı aşıyor.");

    public static Error LoanMonthlyPaymentInvalid() =>
        Error.Validation("LoanMonthlyPaymentInvalid", "Aylık taksit tutarı 0'dan büyük olmalıdır.");

    public static Error LoanMonthlyPaymentScale() =>
        Error.Validation("LoanMonthlyPaymentScale", "Aylık taksit tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error LoanMonthlyPaymentTooLarge() =>
        Error.Validation("LoanMonthlyPaymentTooLarge", "Aylık taksit tutarı izin verilen üst sınırı aşıyor.");

    public static Error LoanInstallmentCountInvalid() =>
        Error.Validation("LoanInstallmentCountInvalid", "Taksit sayısı 1 ile 120 arasında olmalıdır.");

    public static Error LoanFirstDueDateRequired() =>
        Error.Validation("LoanFirstDueDateRequired", "İlk vade tarihi zorunludur.");

    public static Error LoanFirstDueDateBeforeToday() =>
        Error.Validation("LoanFirstDueDateBeforeToday", "İlk vade tarihi kredi tarihinden önce olamaz.");

    public static Error LoanTotalRepaymentBelowPrincipal() =>
        Error.Validation(
            "LoanTotalRepaymentBelowPrincipal",
            "Toplam geri ödeme (aylık taksit × taksit sayısı) kredi tutarından küçük olamaz.");

    public static Error LoanBankNameTooLong() =>
        Error.Validation(
            "LoanBankNameTooLong",
            $"Banka adı en fazla {Loan.BankNameMaxLength} karakter olabilir.");

    public static Error LoanDescriptionTooLong() =>
        Error.Validation(
            "LoanDescriptionTooLong",
            $"Açıklama en fazla {Loan.DescriptionMaxLength} karakter olabilir.");

    public static Error LoanCurrencyUnsupported() =>
        Error.Validation("LoanCurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    public static Error DebtInflowRequired() =>
        Error.Validation("DebtInflowRequired", "Bağlı para girişi kimliği zorunludur.");

    public static Error DebtInflowAlreadyLinked() =>
        Error.Conflict("StateConflict", "Borç zaten bir para girişine bağlı.");

    public static Error LoanNotFound() =>
        Error.NotFound("NotFound", "Kredi bulunamadı.");

    public static Error LoanCancelled() =>
        Error.Conflict("StateConflict", "İptal edilmiş kredi değiştirilemez.");

    public static Error LoanHasPaidInstallments() =>
        Error.Conflict("StateConflict", "Ödenmiş taksit varken kredi iptal edilemez.");

    public static Error InstallmentNotFound() =>
        Error.NotFound("NotFound", "Taksit bulunamadı.");

    public static Error InstallmentCancelled() =>
        Error.Conflict("StateConflict", "İptal edilmiş taksit ödenemez.");

    public static Error InstallmentAlreadyPaid() =>
        Error.Conflict("StateConflict", "Taksit zaten ödenmiş.");

    public static Error InstallmentNotPaid() =>
        Error.Conflict("StateConflict", "Ödenmemiş taksitin ödemesi iptal edilemez.");

    public static Error InstallmentsOutOfOrder() =>
        Error.Conflict("StateConflict", "Önceki taksitler ödenmeden sonraki taksit ödenemez.");

    public static Error InstallmentPaymentNotFound() =>
        Error.NotFound("NotFound", "Taksit ödemesi bulunamadı.");

    public static Error InstallmentPaymentAlreadyCancelled() =>
        Error.Conflict("StateConflict", "Taksit ödemesi zaten iptal edilmiş.");

    public static Error PaymentDateRequired() =>
        Error.Validation("PaymentDateRequired", "Ödeme tarihi zorunludur.");

    public static Error PaymentDateInFuture() =>
        Error.Validation("PaymentDateInFuture", "Ödeme tarihi bugünden ileri olamaz.");

    public static Error PersonalDebtCounterpartyRequired() =>
        Error.Validation("CounterpartyNameRequired", "Borç alınan kişi/kurum zorunludur.");

    public static Error PersonalDebtCounterpartyTooLong() =>
        Error.Validation(
            "CounterpartyNameTooLong",
            $"Borç alınan kişi/kurum en fazla {PersonalDebt.CounterpartyNameMaxLength} karakter olabilir.");

    public static Error PersonalDebtPrincipalInvalid() =>
        Error.Validation("PrincipalInvalid", "Borç tutarı 0'dan büyük olmalıdır.");

    public static Error PersonalDebtPrincipalScale() =>
        Error.Validation("PrincipalScale", "Borç tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error PersonalDebtPrincipalTooLarge() =>
        Error.Validation("PrincipalTooLarge", "Borç tutarı izin verilen üst sınırı aşıyor.");

    public static Error PersonalDebtBorrowedOnRequired() =>
        Error.Validation("BorrowedOnRequired", "Borçlanma tarihi zorunludur.");

    public static Error PersonalDebtBorrowedOnInFuture() =>
        Error.Validation("BorrowedOnInFuture", "Borçlanma tarihi bugünden ileri olamaz.");

    public static Error PersonalDebtDueDateBeforeBorrowedOn() =>
        Error.Validation("DueDateBeforeBorrowedOn", "Vade tarihi borçlanma tarihinden önce olamaz.");

    public static Error PersonalDebtDescriptionTooLong() =>
        Error.Validation(
            "DescriptionTooLong",
            $"Açıklama en fazla {PersonalDebt.DescriptionMaxLength} karakter olabilir.");

    public static Error PersonalDebtCurrencyUnsupported() =>
        Error.Validation("CurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    public static Error PersonalDebtNotFound() =>
        Error.NotFound("NotFound", "Kişisel borç bulunamadı.");

    public static Error PersonalDebtCancelled() =>
        Error.Conflict("StateConflict", "İptal edilmiş borç değiştirilemez.");

    public static Error PersonalDebtHasActiveRepayments() =>
        Error.Conflict("StateConflict", "Aktif geri ödeme varken borç iptal edilemez.");

    public static Error RepaymentAmountInvalid() =>
        Error.Validation("RepaymentAmountInvalid", "Geri ödeme tutarı 0'dan büyük olmalıdır.");

    public static Error RepaymentAmountScale() =>
        Error.Validation("RepaymentAmountScale", "Geri ödeme tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error RepaymentAmountTooLarge() =>
        Error.Validation("RepaymentAmountTooLarge", "Geri ödeme tutarı izin verilen üst sınırı aşıyor.");

    public static Error RepaymentExceedsRemaining() =>
        Error.Validation("RepaymentExceedsRemaining", "Geri ödeme kalan borç tutarını aşamaz.");

    public static Error RepaymentNoteTooLong() =>
        Error.Validation(
            "RepaymentNoteTooLong",
            $"Not en fazla {PersonalDebtRepayment.NoteMaxLength} karakter olabilir.");

    public static Error RepaymentNotFound() =>
        Error.NotFound("NotFound", "Geri ödeme bulunamadı.");

    public static Error RepaymentAlreadyCancelled() =>
        Error.Conflict("StateConflict", "Geri ödeme zaten iptal edilmiş.");

    public static Error CancellationReasonRequired() =>
        Error.Validation("CancellationReasonRequired", "İptal gerekçesi zorunludur.");

    public static Error CancellationReasonTooLong() =>
        Error.Validation("CancellationReasonTooLong", "İptal gerekçesi en fazla 500 karakter olabilir.");

    public static Error? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return CancellationReasonRequired();
        }

        return reason.Trim().Length > 500 ? CancellationReasonTooLong() : null;
    }

    /// <summary><c>amount &gt; 0</c> ve <c>decimal(19,4)</c> sınırları.</summary>
    public static Error? ValidatePositiveAmount(
        decimal amount,
        Func<Error> invalid,
        Func<Error> scale,
        Func<Error> tooLarge)
    {
        if (amount <= 0m)
        {
            return invalid();
        }

        if (decimal.Round(amount, MaxAmountScale) != amount)
        {
            return scale();
        }

        return amount > MaxAmount ? tooLarge() : null;
    }

    public static Error? ValidateCurrency(string? currency, Func<Error> unsupported)
    {
        var code = string.IsNullOrWhiteSpace(currency)
            ? Money.DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        return string.Equals(code, Money.DefaultCurrency, StringComparison.Ordinal)
            ? null
            : unsupported();
    }

    public static string NormalizeCurrency(string? currency) => string.IsNullOrWhiteSpace(currency)
        ? Money.DefaultCurrency
        : currency.Trim().ToUpperInvariant();
}
