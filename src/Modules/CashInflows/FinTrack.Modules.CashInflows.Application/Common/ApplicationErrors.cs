using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Application.Common;

/// <summary>
/// Application katmanının ürettiği altyapı bağımsız hatalar.
/// Domain hataları kendi kodlarını taşır; burada yalnızca akış hataları vardır.
/// </summary>
internal static class ApplicationErrors
{
    public static Error CashInflowNotFound() =>
        Error.NotFound("NotFound", "Para girişi bulunamadı.");

    public static Error CashInflowSourceRequired() =>
        Error.Validation("CashInflowSourceRequired", "Para girişi kaynağı zorunludur.");

    public static Error Concurrency() =>
        Error.Conflict("ConcurrencyConflict", "Kayıt başka bir işlemle güncellenmiş. Lütfen yeniden yükleyin.");

    public static Error InvalidRowVersion() =>
        Error.Validation("RowVersionInvalid", "rowVersion zorunludur ve geçerli bir base64 değer olmalıdır.");

    public static Error DuplicateKey() =>
        Error.Conflict("StateConflict", "Aynı kayıt zaten mevcut.");

    public static Error DebtInflowKindInvalid() =>
        Error.Validation("DebtInflowKindInvalid", "Geçerli bir borç kaynağı seçilmelidir.");

    public static Error DebtInflowLinkMissing() =>
        Error.Conflict("StateConflict", "Para girişi borç bağlantılı değil.");

    public static Error DebtInflowLinkMismatch() =>
        Error.Conflict("StateConflict", "Para girişi beklenen borç kaydına bağlı değil.");
}
