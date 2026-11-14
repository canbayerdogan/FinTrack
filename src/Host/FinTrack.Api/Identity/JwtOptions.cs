using System.Security.Cryptography;
using System.Text;

namespace FinTrack.Api.Identity;

/// <summary>
/// JWT ayarları. SigningKey repoya yazılmaz; üretimde <c>Jwt__SigningKey</c> ortam değişkeni zorunludur.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "FinTrack";

    public string Audience { get; set; } = "FinTrack.Mobile";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>
    /// Geliştirmede anahtar yoksa süreç ömrüne bağlı rastgele anahtar üretir.
    /// Üretimde anahtar zorunludur ve en az 32 bayt olmalıdır.
    /// </summary>
    public static JwtOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                // Sır değildir ve loglanmaz; yalnızca geliştirme oturumunda geçerlidir.
                options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            }
            else
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey yapılandırılmalıdır. Üretimde ortam değişkeni olarak verin (Jwt__SigningKey).");
            }
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey en az 32 bayt olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer ve Jwt:Audience boş olamaz.");
        }

        if (options.AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes sıfırdan büyük olmalıdır.");
        }

        if (options.RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Jwt:RefreshTokenDays sıfırdan büyük olmalıdır.");
        }

        return options;
    }
}
