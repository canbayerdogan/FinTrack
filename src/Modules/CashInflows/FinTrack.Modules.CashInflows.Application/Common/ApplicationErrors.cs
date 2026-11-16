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
}
