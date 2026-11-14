using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Domain;

/// <summary>
/// Domain invariant hataları. Kodlar API katmanında alan/HTTP eşlemesinde kullanılır;
/// mesajlar kullanıcıya gösterilecek Türkçe metinlerdir.
/// </summary>
internal static class DomainErrors
{
    public static Error UserRequired() =>
        Error.Validation("UserRequired", "Kullanıcı kimliği zorunludur.");

    public static Error CategoryNameRequired() =>
        Error.Validation("CategoryNameRequired", "Kategori adı zorunludur.");

    public static Error CategoryNameTooLong() =>
        Error.Validation("CategoryNameTooLong", $"Kategori adı en fazla {Category.NameMaxLength} karakter olabilir.");

    public static Error CategoryIconTooLong() =>
        Error.Validation("CategoryIconTooLong", $"Simge anahtarı en fazla {Category.IconKeyMaxLength} karakter olabilir.");

    public static Error ExpenseAmountInvalid() =>
        Error.Validation("ExpenseAmountInvalid", "Tutar 0'dan büyük olmalıdır.");

    public static Error ExpenseAmountScale() =>
        Error.Validation("ExpenseAmountScale", "Tutar en fazla 4 ondalık basamak içerebilir.");

    public static Error ExpenseAmountTooLarge() =>
        Error.Validation("ExpenseAmountTooLarge", "Tutar izin verilen üst sınırı aşıyor.");

    public static Error ExpenseCurrencyUnsupported() =>
        Error.Validation("ExpenseCurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    public static Error ExpenseDateRequired() =>
        Error.Validation("ExpenseDateRequired", "Harcama tarihi zorunludur.");

    public static Error ExpenseDateInFuture() =>
        Error.Validation("ExpenseDateInFuture", "Harcama tarihi bugünden ileri olamaz.");

    public static Error ExpenseDescriptionTooLong() =>
        Error.Validation("ExpenseDescriptionTooLong", $"Açıklama en fazla {Expense.DescriptionMaxLength} karakter olabilir.");

    public static Error ExpensePaymentMethodInvalid() =>
        Error.Validation("ExpensePaymentMethodInvalid", "Geçerli bir ödeme yöntemi seçilmelidir.");

    public static Error ExpenseCategoryRequired() =>
        Error.Validation("ExpenseCategoryRequired", "Kategori zorunludur.");

    public static Error ExpenseCardRequired() =>
        Error.Validation("ExpenseCardRequired", "Kredi kartı ödemesinde kart zorunludur.");

    public static Error ExpenseCardNotAllowed() =>
        Error.Validation("ExpenseCardNotAllowed", "Kart yalnızca kredi kartı ödemesinde verilebilir.");

    public static Error ExpenseCancelled(string message = "İptal edilmiş harcama güncellenemez.") =>
        Error.Conflict("ExpenseCancelled", message);

    public static Error ExpenseHasActiveRefunds() =>
        Error.Conflict("ExpenseHasActiveRefunds", "Aktif iadeler varken harcama iptal edilemez; önce iadeleri iptal edin.");

    public static Error ExpenseAmountBelowRefunds() =>
        Error.Validation("ExpenseAmountBelowRefunds", "Tutar, aktif iadeler toplamının altına düşürülemez.");

    public static Error ExpenseDateBeforeRefunds() =>
        Error.Validation("ExpenseDateBeforeRefunds", "Harcama tarihi, aktif iadelerin tarihinden sonra olamaz.");

    public static Error CancellationReasonRequired() =>
        Error.Validation("CancellationReasonRequired", "İptal gerekçesi zorunludur.");

    public static Error CancellationReasonTooLong() =>
        Error.Validation("CancellationReasonTooLong", $"İptal gerekçesi en fazla {Expense.CancellationReasonMaxLength} karakter olabilir.");

    public static Error RefundAmountInvalid() =>
        Error.Validation("RefundAmountInvalid", "İade tutarı 0'dan büyük olmalıdır.");

    public static Error RefundAmountScale() =>
        Error.Validation("RefundAmountScale", "İade tutarı en fazla 4 ondalık basamak içerebilir.");

    public static Error RefundAmountTooLarge() =>
        Error.Validation("RefundAmountTooLarge", "İade tutarı izin verilen üst sınırı aşıyor.");

    public static Error RefundExceedsExpense() =>
        Error.Validation("RefundExceedsExpense", "Aktif iadeler toplamı harcama tutarını aşamaz.");

    public static Error RefundDateRequired() =>
        Error.Validation("RefundDateRequired", "İade tarihi zorunludur.");

    public static Error RefundDateBeforeExpense() =>
        Error.Validation("RefundDateBeforeExpense", "İade tarihi harcama tarihinden önce olamaz.");

    public static Error RefundDateInFuture() =>
        Error.Validation("RefundDateInFuture", "İade tarihi bugünden ileri olamaz.");

    public static Error RefundDescriptionTooLong() =>
        Error.Validation("RefundDescriptionTooLong", $"İade açıklaması en fazla {Refund.DescriptionMaxLength} karakter olabilir.");

    public static Error RefundNotFound() =>
        Error.NotFound("NotFound", "İade bulunamadı.");

    public static Error? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return CancellationReasonRequired();
        }

        return reason.Trim().Length > Expense.CancellationReasonMaxLength
            ? CancellationReasonTooLong()
            : null;
    }

    public static Error? ValidateAmount(decimal amount, Func<Error> invalid, Func<Error> scale, Func<Error> tooLarge)
    {
        if (amount <= 0m)
        {
            return invalid();
        }

        if (decimal.Round(amount, 4) != amount)
        {
            return scale();
        }

        return amount > Expense.MaxAmount ? tooLarge() : null;
    }
}
