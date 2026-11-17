using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Mobile.Core.Abstractions;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// 401 yanıtında refresh token döndürme. Refresh isteği auth handler
/// olmadan ayrı HttpClient ile yapılır; böylece geri dönüşsüz döngü olmaz.
/// </summary>
public sealed class SessionRefresher
{
    private readonly HttpClient _authClient;
    private readonly ITokenStore _tokenStore;

    public SessionRefresher(HttpClient authClient, ITokenStore tokenStore)
    {
        _authClient = authClient;
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Refresh başarısız olduğunda oturumun sonlandığını bildirir.
    /// </summary>
    public event Func<Task>? SessionExpired;

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            await RaiseSessionExpiredAsync();
            return false;
        }

        HttpResponseMessage? response = null;
        try
        {
            response = await _authClient.PostAsJsonAsync(
                "auth/refresh",
                new RefreshRequest(refreshToken),
                ApiJson.Options,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await RaiseSessionExpiredAsync();
                return false;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options, cancellationToken);
            if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken) ||
                string.IsNullOrWhiteSpace(auth.RefreshToken))
            {
                await RaiseSessionExpiredAsync();
                return false;
            }

            await _tokenStore.SaveAsync(
                new TokenPair(auth.AccessToken, auth.RefreshToken),
                cancellationToken);
            return true;
        }
        catch (HttpRequestException)
        {
            await RaiseSessionExpiredAsync();
            return false;
        }
        catch (JsonException)
        {
            await RaiseSessionExpiredAsync();
            return false;
        }
        finally
        {
            response?.Dispose();
        }
    }

    public async Task RaiseSessionExpiredAsync()
    {
        await _tokenStore.ClearAsync();
        if (SessionExpired is not null)
        {
            await SessionExpired();
        }
    }

    private sealed record RefreshRequest(string RefreshToken);

    private sealed record AuthResponse(string AccessToken, string RefreshToken);
}
