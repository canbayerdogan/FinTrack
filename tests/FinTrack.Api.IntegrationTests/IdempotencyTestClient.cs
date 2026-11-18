using Microsoft.AspNetCore.Mvc.Testing;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Mevcut entegrasyon testlerinin mutasyon POST'larına otomatik, benzersiz
/// Idempotency-Key ekler. Auth POST'ları kapsam dışıdır; aynı anahtarın
/// farklı isteklerde tekrar kullanılmaması için her istekte yeni UUID üretilir.
/// </summary>
internal sealed class IdempotencyKeyHandler : DelegatingHandler
{
    private static readonly string[] InScopePrefixes =
    [
        "/api/v1/expenses",
        "/api/v1/categories",
        "/api/v1/cash-inflows",
        "/api/v1/cards",
        "/api/v1/personal-debts",
        "/api/v1/loans",
    ];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (!request.Headers.Contains("Idempotency-Key") &&
            request.Method == HttpMethod.Post &&
            !path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase) &&
            InScopePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("D"));
        }

        return base.SendAsync(request, cancellationToken);
    }
}

internal static class IdempotencyTestClient
{
    public static HttpClient Create(WebApplicationFactory<Program> factory)
    {
        var handler = new IdempotencyKeyHandler { InnerHandler = factory.Server.CreateHandler() };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost"),
        };
    }

    public static HttpClient CreatePlain(WebApplicationFactory<Program> factory) =>
        new(factory.Server.CreateHandler(), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost"),
        };
}
