using FinTrack.SharedKernel;

namespace FinTrack.Modules.Debts.Application.Common;

/// <summary>
/// Application katmanının ürettiği altyapı bağımsız hatalar. Domain hataları
/// kendi kodlarını taşır; burada yalnızca akış/altyapı çevirisi vardır.
/// </summary>
internal static class ApplicationErrors
{
    public static Error Concurrency() =>
        Error.Conflict("ConcurrencyConflict", "Kayıt başka bir işlemle güncellenmiş. Lütfen yeniden yükleyin.");

    public static Error InvalidRowVersion() =>
        Error.Validation("RowVersionInvalid", "rowVersion zorunludur ve geçerli bir base64 değer olmalıdır.");

    public static Error DuplicateKey() =>
        Error.Conflict("StateConflict", "Aynı kayıt zaten mevcut.");

    public static Error LoanNotFound() =>
        Error.NotFound("NotFound", "Kredi bulunamadı.");

    public static Error LoanInflowMissing() =>
        Error.Conflict("StateConflict", "Kredinin bağlı para girişi yok.");

    public static Error PersonalDebtNotFound() =>
        Error.NotFound("NotFound", "Kişisel borç bulunamadı.");

    public static Error PersonalDebtInflowMissing() =>
        Error.Conflict("StateConflict", "Kişisel borcun bağlı para girişi yok.");

    public static Error InstallmentPaymentNotFound() =>
        Error.NotFound("NotFound", "Taksit ödemesi bulunamadı.");

    public static Error RepaymentNotFound() =>
        Error.NotFound("NotFound", "Geri ödeme bulunamadı.");
}
