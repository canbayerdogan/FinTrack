namespace FinTrack.SharedKernel;

/// <summary>
/// Kullanıcının yerel takvim günü. <c>OccurredOn</c>/<c>RefundedOn</c> gibi
/// <see cref="DateOnly"/> alanları saat dilimi kayması olmadan karşılaştırmak için
/// kullanılır (docs/05 §2.2).
/// </summary>
public static class UserTime
{
    public const string DefaultTimeZoneId = "Europe/Istanbul";

    /// <summary>
    /// IANA kimliğini çözer. Geçersiz veya boş kimlikte varsayılana düşer;
    /// böylece bozuk bir token tüm isteği 500'e çevirmez.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId)
            ? DefaultTimeZoneId
            : timeZoneId.Trim();

        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone))
        {
            return timeZone;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(DefaultTimeZoneId, out var fallback)
            ? fallback
            : TimeZoneInfo.Utc;
    }

    /// <summary>UTC saati kullanıcının saat diliminde takvim gününe çevirir.</summary>
    public static DateOnly Today(IClock clock, string? timeZoneId)
    {
        var utcNow = DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ResolveTimeZone(timeZoneId));
        return DateOnly.FromDateTime(local);
    }
}
