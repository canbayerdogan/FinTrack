namespace FinTrack.Mobile.Core.Abstractions;

/// <summary>
/// Access/refresh token çifti. Token değerleri hiçbir zaman loglanmaz veya
/// hata mesajlarına eklenmez.
/// </summary>
public sealed record TokenPair(string AccessToken, string RefreshToken);

/// <summary>
/// Platforma özgü güvenli depolama (Android Keystore / Windows Credential Manager)
/// için çekirdek katmanın gördüğü sözleşme.
/// </summary>
public interface ITokenStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(TokenPair tokens, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
