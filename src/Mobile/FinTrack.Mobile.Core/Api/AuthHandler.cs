using System.Net;
using FinTrack.Mobile.Core.Abstractions;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// Her isteğe Bearer token ekler; 401 alındığında tek sefer refresh dener.
/// Eşzamanlı isteklerde refresh tek uçuş (single-flight) ile yapılır.
/// Idempotency-Key zorunluluğu v1.1'e ertelendiği için otomatik tekrar yalnızca
/// güvenli (GET/HEAD) isteklerde yapılır; POST/PUT kullanıcıya bırakılır.
/// </summary>
public sealed class AuthHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly SessionRefresher _refresher;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public AuthHandler(ITokenStore tokenStore, SessionRefresher refresher)
    {
        _tokenStore = tokenStore;
        _refresher = refresher;
        _refresher.SessionExpired += OnSessionExpired;
    }

    public event Func<Task>? SessionExpired;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await _tokenStore.GetAccessTokenAsync(cancellationToken);
        AttachToken(request, accessToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || IsAuthRequest(request))
        {
            return response;
        }

        bool refreshed;
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var currentToken = await _tokenStore.GetAccessTokenAsync(cancellationToken);
            refreshed = !string.Equals(currentToken, accessToken, StringComparison.Ordinal) ||
                        await _refresher.TryRefreshAsync(cancellationToken);
        }
        finally
        {
            _refreshGate.Release();
        }

        if (!refreshed)
        {
            // SessionRefresher oturum sonu olayını zaten yükseltti.
            return response;
        }

        if (!IsRetryable(request))
        {
            // Token yenilendi ama POST/PUT otomatik tekrar gönderilmez
            // (docs/08 §6: Idempotency-Key ertelendi).
            return response;
        }

        response.Dispose();
        var retry = CloneRequest(request);
        var newToken = await _tokenStore.GetAccessTokenAsync(cancellationToken);
        AttachToken(retry, newToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refresher.SessionExpired -= OnSessionExpired;
            _refreshGate.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void AttachToken(HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private static bool IsAuthRequest(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        return path.Contains("/auth/", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/auth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetryable(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get ||
        request.Method == HttpMethod.Head ||
        request.Method == HttpMethod.Options;

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private Task OnSessionExpired() => RaiseSessionExpiredAsync();

    private async Task RaiseSessionExpiredAsync()
    {
        if (SessionExpired is not null)
        {
            await SessionExpired();
        }
    }
}
