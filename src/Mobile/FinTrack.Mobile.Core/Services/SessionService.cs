using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;

namespace FinTrack.Mobile.Core.Services;

/// <summary>
/// Oturum durumunu ve token yaşam döngüsünü ViewModel'lere sunar.
/// </summary>
public sealed class SessionService
{
    private readonly FinTrackApiClient _api;
    private readonly ITokenStore _tokenStore;

    public SessionService(FinTrackApiClient api, ITokenStore tokenStore)
    {
        _api = api;
        _tokenStore = tokenStore;
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrWhiteSpace(await _tokenStore.GetAccessTokenAsync(cancellationToken));

    public async Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var auth = await _api.LoginAsync(email, password, cancellationToken);
        await _tokenStore.SaveAsync(new TokenPair(auth.AccessToken, auth.RefreshToken), cancellationToken);
    }

    public async Task RegisterAsync(
        string email,
        string password,
        string preferredTimeZone,
        CancellationToken cancellationToken = default)
    {
        var auth = await _api.RegisterAsync(email, password, preferredTimeZone, cancellationToken);
        await _tokenStore.SaveAsync(new TokenPair(auth.AccessToken, auth.RefreshToken), cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await _api.LogoutAsync(refreshToken, cancellationToken);
            }
            catch (ApiException)
            {
                // Yerel oturum her durumda kapatılır.
            }
            catch (HttpRequestException)
            {
                // Çevrimdışı çıkışta yerel token yine silinir.
            }
        }

        await _tokenStore.ClearAsync(cancellationToken);
    }
}
