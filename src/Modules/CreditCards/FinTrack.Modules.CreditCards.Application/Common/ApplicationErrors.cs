using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Common;

/// <summary>
/// Application katmanının ürettiği altyapı bağımsız hatalar.
/// Domain hataları kendi kodlarını taşır; burada yalnızca akış hataları vardır.
/// </summary>
internal static class ApplicationErrors
{
    public static Error CardNotFound() =>
        Error.NotFound("NotFound", "Kart bulunamadı.");

    public static Error Concurrency() =>
        Error.Conflict("ConcurrencyConflict", "Kayıt başka bir işlemle güncellenmiş. Lütfen yeniden yükleyin.");

    public static Error InvalidRowVersion() =>
        Error.Validation("RowVersionInvalid", "rowVersion zorunludur ve geçerli bir base64 değer olmalıdır.");

    public static Error DuplicateKey() =>
        Error.Conflict("StateConflict", "Aynı kayıt zaten mevcut.");

    public static Error CardPaymentNotFound() =>
        Error.NotFound("NotFound", "Kart ödemesi bulunamadı.");

    public static Error CashAdvanceNotFound() =>
        Error.NotFound("NotFound", "Nakit avans bulunamadı.");

    public static Error CashAdvanceInflowMissing() =>
        Error.Conflict("StateConflict", "Nakit avansın bağlı para girişi yok.");

    public static Error StatementNotFound() =>
        Error.NotFound("NotFound", "Ekstre bulunamadı.");

    public static Error CardArchived() =>
        Error.Validation("CardArchived", "Arşivlenmiş karta yeni kayıt eklenemez.");

    public static Error AsOfInFuture() =>
        Error.Validation("AsOfInFuture", "asOf, kullanıcının bugünkü tarihinden sonra olamaz.");

    public static Error StatementPeriodOverlap() =>
        Error.Validation("StatementPeriodOverlap", "Bu dönem, aynı kartta aktif bir ekstre dönemiyle çakışıyor.");
}
