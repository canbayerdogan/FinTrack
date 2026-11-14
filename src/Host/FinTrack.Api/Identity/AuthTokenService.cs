using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Identity;

/// <summary>
/// Access + rotating refresh token çifti üretir.
/// Refresh token düz metni yalnızca yanıtta döner; veritabanında SHA-256 özeti tutulur.
/// </summary>
public sealed class AuthTokenService(
    FinTrackIdentityDbContext db,
    JwtTokenService jwt,
    IClock clock,
    JwtOptions options)
{
    public async Task<AuthResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var accessToken = jwt.CreateAccessToken(user);
        var refreshToken = JwtTokenService.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            CreatedAtUtc = clock.UtcNow,
            ExpiresAtUtc = clock.UtcNow.AddDays(options.RefreshTokenDays),
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(user, accessToken, refreshToken);
    }

    /// <summary>
    /// Kullanılan refresh token'ı iptal eder ve yerine yenisini üretir.
    /// Token geçersiz, süresi dolmuş veya daha önce kullanılmışsa <c>null</c>.
    /// </summary>
    public async Task<AuthResponse?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(
            t => t.TokenHash == hash,
            cancellationToken);

        if (existing is null || !existing.IsActive(clock.UtcNow))
        {
            return null;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == existing.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var accessToken = jwt.CreateAccessToken(user);
        var newRefreshToken = JwtTokenService.CreateRefreshToken();
        var newHash = JwtTokenService.HashRefreshToken(newRefreshToken);

        existing.RevokedAtUtc = clock.UtcNow;
        existing.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            CreatedAtUtc = clock.UtcNow,
            ExpiresAtUtc = clock.UtcNow.AddDays(options.RefreshTokenDays),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Aynı token iki istekte birlikte kullanıldı; yarışı kaybeden istek reddedilir.
            return null;
        }

        return ToResponse(user, accessToken, newRefreshToken);
    }

    /// <summary>Logout idempotenttir: token zaten geçersizse de hata döndürmez.</summary>
    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(
            t => t.TokenHash == hash,
            cancellationToken);

        if (existing is null || existing.RevokedAtUtc is not null)
        {
            return;
        }

        existing.RevokedAtUtc = clock.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Başka bir istek zaten iptal etti; logout yine başarılı sayılır.
        }
    }

    private static AuthResponse ToResponse(ApplicationUser user, AccessToken accessToken, string refreshToken) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.PreferredTimeZone,
            accessToken.Token,
            refreshToken,
            accessToken.ExpiresInSeconds,
            "Bearer");
}
