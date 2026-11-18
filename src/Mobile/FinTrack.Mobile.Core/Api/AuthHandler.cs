using System.Net;
using System.Net.Http.Headers;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.PendingOperations;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// Her isteğe Bearer token ekler; 401 alındığında tek sefer refresh dener.
/// Eşzamanlı isteklerde refresh tek uçuş (single-flight) ile yapılır.
/// GET/HEAD/OPTIONS her zaman tekrar edilir; POST yalnızca pending-operation
/// kaydı SentUnconfirmed ise ve aynı Idempotency-Key/gövde baytlarıyla tekrar
/// edilir. Auth POST'ları ve PUT hiçbir zaman otomatik tekrar edilmez.
/// </summary>
public sealed class AuthHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly SessionRefresher _refresher;
    private readonly IPendingOperationStore? _pendingOperations;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public AuthHandler(
        ITokenStore tokenStore,
        SessionRefresher refresher,
        IPendingOperationStore? pendingOperations = null)
    {
        _tokenStore = tokenStore;
        _refresher = refresher;
        _pendingOperations = pendingOperations;
        _refresher.SessionExpired += OnSessionExpired;
    }

    public event Func<Task>? SessionExpired;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        byte[]? postBody = null;
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>>? postHeaders = null;
        string? idempotencyKey = null;
        if (request.Method == HttpMethod.Post && !IsAuthRequest(request))
        {
            idempotencyKey = TryGetIdempotencyKey(request);
            if (idempotencyKey is not null && request.Content is not null)
            {
                // Gövde baytları ilk gönderimden önce bir kez okunur; retry aynı
                // baytları kullanır (docs/11 §7.4).
                postBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                postHeaders = request.Content.Headers
                    .Select(header => new KeyValuePair<string, IEnumerable<string>>(
                        header.Key,
                        header.Value.ToArray()))
                    .ToArray();
            }
        }

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

        if (!refreshed || !await IsRetryableAsync(request, idempotencyKey, cancellationToken))
        {
            // Token yenilendi ama istek güvenli değilse tekrar gönderilmez.
            return response;
        }

        response.Dispose();
        var retry = CloneRequest(request);
        if (postBody is not null)
        {
            retry.Content = CreateBodyContent(postBody, postHeaders);
        }

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
                new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private static bool IsAuthRequest(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        return path.Contains("/auth/", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/auth", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetIdempotencyKey(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues("Idempotency-Key", out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Guid.TryParse(value, out var parsed) ? parsed.ToString("D") : value;
    }

    private async Task<bool> IsRetryableAsync(
        HttpRequestMessage request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get ||
            request.Method == HttpMethod.Head ||
            request.Method == HttpMethod.Options)
        {
            return true;
        }

        if (request.Method != HttpMethod.Post ||
            idempotencyKey is null ||
            _pendingOperations is null ||
            IsAuthRequest(request))
        {
            return false;
        }

        var operation = await _pendingOperations.GetAsync(idempotencyKey, cancellationToken);
        return operation?.State == PendingOperationState.SentUnconfirmed;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static ByteArrayContent CreateBodyContent(
        byte[] body,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>>? headers)
    {
        var content = new ByteArrayContent(body);
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return content;
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
