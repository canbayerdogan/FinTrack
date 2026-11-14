namespace FinTrack.Api.Identity;

/// <summary>
/// Rotating refresh token kaydı. Token'ın kendisi saklanmaz; yalnızca SHA-256 özeti.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Token'ın SHA-256 özeti (hex, 64 karakter). Düz metin token asla saklanmaz.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Rotasyon sonrası yerine geçen token'ın özeti; izlenebilirlik için.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Eşzamanlı rotasyon yarışını yakalamak için SQL Server <c>rowversion</c>.</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}
