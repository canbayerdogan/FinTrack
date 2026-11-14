using Microsoft.AspNetCore.Identity;

namespace FinTrack.Api.Identity;

/// <summary>
/// FinTrack kullanıcısı. Identity tabanlıdır; finansal veri taşımaz.
/// <see cref="RowVersion"/> API sözleşmesindeki base64 sürüm değerinin kaynağıdır.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const string DefaultTimeZone = "Europe/Istanbul";

    /// <summary>IANA saat dilimi kimliği. Raporlarda ay sınırları bu dilime göre hesaplanır.</summary>
    public string PreferredTimeZone { get; set; } = DefaultTimeZone;

    /// <summary>Kayıt anı; her zaman UTC.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>SQL Server <c>rowversion</c> sütunu. İstemciye base64 olarak döner.</summary>
    public byte[] RowVersion { get; set; } = [];
}
