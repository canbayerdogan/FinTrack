using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Domain;

/// <summary>
/// Domain invariant hataları. Kodlar API katmanında alan/HTTP eşlemesinde kullanılır;
/// mesajlar kullanıcıya gösterilecek Türkçe metinlerdir.
/// </summary>
internal static class DomainErrors
{
    public static Error UserRequired() =>
        Error.Validation("UserRequired", "Kullanıcı kimliği zorunludur.");

    public static Error CashInflowAmountInvalid() =>
        Error.Validation("CashInflowAmountInvalid", "Tutar 0'dan büyük olmalıdır.");

    public static Error CashInflowAmountScale() =>
        Error.Validation("CashInflowAmountScale", "Tutar en fazla 4 ondalık basamak içerebilir.");

    public static Error CashInflowAmountTooLarge() =>
        Error.Validation("CashInflowAmountTooLarge", "Tutar izin verilen üst sınırı aşıyor.");

    public static Error CashInflowCurrencyUnsupported() =>
        Error.Validation("CashInflowCurrencyUnsupported", "v1'de yalnızca TRY desteklenir.");

    public static Error CashInflowDateRequired() =>
        Error.Validation("CashInflowDateRequired", "Para girişi tarihi zorunludur.");

    public static Error CashInflowDateInFuture() =>
        Error.Validation("CashInflowDateInFuture", "Para girişi tarihi bugünden ileri olamaz.");

    public static Error CashInflowSourceInvalid() =>
        Error.Validation("CashInflowSourceInvalid", "Geçerli bir para girişi kaynağı seçilmelidir.");

    public static Error CashInflowDescriptionTooLong() =>
        Error.Validation(
            "CashInflowDescriptionTooLong",
            $"Açıklama en fazla {CashInflow.DescriptionMaxLength} karakter olabilir.");

    /// <summary>
    /// Borç doğuran kaynaklar yalnızca sahibi olan uç noktadan oluşturulabilir
    /// (docs/05 §4.3, docs/06 §5.1).
    /// </summary>
    public static Error DebtSourceRequiresOwnerEndpoint() =>
        Error.Validation(
            "DebtSourceRequiresOwnerEndpoint",
            "Bu kaynak borç doğurur; ilgili borç veya kart uç noktasından oluşturulmalıdır.");

    public static Error CashInflowCancelled() =>
        Error.Conflict("StateConflict", "İptal edilmiş para girişi güncellenemez.");

    public static Error CashInflowDebtLinked() =>
        Error.Conflict("StateConflict", "Borç bağlantılı para girişi bu uç noktadan değiştirilemez veya iptal edilemez.");

    public static Error CancellationReasonRequired() =>
        Error.Validation("CancellationReasonRequired", "İptal gerekçesi zorunludur.");

    public static Error CancellationReasonTooLong() =>
        Error.Validation(
            "CancellationReasonTooLong",
            $"İptal gerekçesi en fazla {CashInflow.CancellationReasonMaxLength} karakter olabilir.");

    public static Error? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return CancellationReasonRequired();
        }

        return reason.Trim().Length > CashInflow.CancellationReasonMaxLength
            ? CancellationReasonTooLong()
            : null;
    }

    public static Error? ValidateAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            return CashInflowAmountInvalid();
        }

        if (decimal.Round(amount, 4) != amount)
        {
            return CashInflowAmountScale();
        }

        return amount > CashInflow.MaxAmount ? CashInflowAmountTooLarge() : null;
    }
}
