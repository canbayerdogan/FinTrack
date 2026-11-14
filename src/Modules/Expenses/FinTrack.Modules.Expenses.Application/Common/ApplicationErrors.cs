using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Common;

/// <summary>
/// Application katmanının ürettiği altyapı bağımsız hatalar.
/// Domain hataları kendi kodlarını taşır; burada yalnızca akış hataları vardır.
/// </summary>
internal static class ApplicationErrors
{
    public static Error CategoryNotFound() =>
        Error.NotFound("NotFound", "Kategori bulunamadı.");

    public static Error ExpenseNotFound() =>
        Error.NotFound("NotFound", "Harcama bulunamadı.");

    public static Error RefundNotFound() =>
        Error.NotFound("NotFound", "İade bulunamadı.");

    public static Error Concurrency() =>
        Error.Conflict("ConcurrencyConflict", "Kayıt başka bir işlemle güncellenmiş. Lütfen yeniden yükleyin.");

    public static Error CategoryNameTaken() =>
        Error.Conflict("StateConflict", "Bu isimde bir kategori zaten var.");

    public static Error InvalidRowVersion() =>
        Error.Validation("RowVersionInvalid", "rowVersion zorunludur ve geçerli bir base64 değer olmalıdır.");

    public static Error DuplicateKey() =>
        Error.Conflict("StateConflict", "Aynı kayıt zaten mevcut.");
}
