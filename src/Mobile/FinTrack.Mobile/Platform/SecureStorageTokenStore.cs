using FinTrack.Mobile.Core.Abstractions;
using Microsoft.Maui.Storage;

namespace FinTrack.Mobile.Platform;

/// <summary>
/// Token'ları Android Keystore / Windows Credential Manager üzerinde saklar.
/// Çekirdek katman yalnızca ITokenStore sözleşmesini bilir.
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string AccessTokenKey = "fintrack.access_token";
    private const string RefreshTokenKey = "fintrack.refresh_token";

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        SecureStorage.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) =>
        SecureStorage.GetAsync(RefreshTokenKey);

    public async Task SaveAsync(TokenPair tokens, CancellationToken cancellationToken = default)
    {
        await SecureStorage.SetAsync(AccessTokenKey, tokens.AccessToken);
        await SecureStorage.SetAsync(RefreshTokenKey, tokens.RefreshToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }
}
