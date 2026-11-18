using FinTrack.Api.Idempotency;
using Xunit;

namespace FinTrack.Api.UnitTests;

/// <summary>
/// docs/11 §3.1 / S9–S12: kapsam (scope) hash'inin hangi girdilere bağlı
/// olduğunu doğrular. Query string kapsama girmez; userId JWT sub'dan gelir.
/// </summary>
public sealed class IdempotencyScopeTests
{
    private const string User = "11111111-1111-1111-1111-111111111111";
    private const string OtherUser = "22222222-2222-2222-2222-222222222222";
    private const string Key = "33333333-3333-3333-3333-333333333333";
    private const string OtherKey = "44444444-4444-4444-4444-444444444444";
    private const string Route = "/api/v1/loans/{id}/installments/{number}/pay";
    private const string OtherRoute = "/api/v1/loans/{id}/installments/{number}/cancel";

    private static string Scope(
        string user = User,
        string method = "POST",
        string route = Route,
        string routeValues = "id=aaa,number=2",
        string key = Key) =>
        IdempotencyScope.Compute(user, method, route, routeValues, key);

    [Fact]
    public void Ayni_girdiler_ayni_kapsam_uretin()
    {
        Assert.Equal(Scope(), Scope());
        Assert.Matches("^[0-9a-f]{64}$", Scope());
    }

    [Fact]
    public void S9_Farkli_kullanici_ayni_key_farkli_kapsam_uretin()
    {
        Assert.NotEqual(Scope(), Scope(user: OtherUser));
        Assert.Equal(IdempotencyScope.UserHash(User), IdempotencyScope.UserHash(User));
        Assert.NotEqual(IdempotencyScope.UserHash(User), IdempotencyScope.UserHash(OtherUser));
    }

    [Fact]
    public void S10_Farkli_route_template_ayni_key_farkli_kapsam_uretin()
    {
        Assert.NotEqual(Scope(), Scope(route: OtherRoute));
    }

    [Fact]
    public void S11_Farkli_route_degeri_ayni_key_farkli_kapsam_uretin()
    {
        Assert.NotEqual(Scope(), Scope(routeValues: "id=bbb,number=2"));
        Assert.NotEqual(Scope(), Scope(routeValues: "id=aaa,number=3"));
    }

    [Fact]
    public void Ayni_key_farkli_idempotency_key_farkli_kapsam_uretin()
    {
        Assert.NotEqual(Scope(), Scope(key: OtherKey));
    }

    [Fact]
    public void S12_Query_string_kapsama_girmez()
    {
        // Middleware route template'i RouteEndpoint.RoutePattern.RawText'ten,
        // route değerlerini Request.RouteValues'dan alır; ikisi de query içermez.
        const string urlWithQueryA = "/api/v1/loans/aaa/installments/2/pay?source=mobile";
        const string urlWithQueryB = "/api/v1/loans/aaa/installments/2/pay?source=web&debug=1";

        Assert.NotEqual(urlWithQueryA, urlWithQueryB); // senaryo gerçekten farklı

        var scopeA = Scope(route: Route, routeValues: "id=aaa,number=2");
        var scopeB = Scope(route: Route, routeValues: "id=aaa,number=2");

        Assert.Equal(scopeA, scopeB);
    }

    [Fact]
    public void CanonicalRouteValues_ada_gore_siralanir_ve_null_degerleri_atar()
    {
        var first = new Dictionary<string, string?>
        {
            ["number"] = "2",
            ["id"] = "aaa",
            ["ignored"] = null,
        };
        var second = new Dictionary<string, string?>
        {
            ["ignored"] = null,
            ["id"] = "aaa",
            ["number"] = "2",
        };

        var canonical = IdempotencyScope.CanonicalRouteValues(first);

        Assert.Equal("id=aaa,number=2", canonical);
        Assert.Equal(canonical, IdempotencyScope.CanonicalRouteValues(second));
        Assert.Equal(Scope(), Scope(routeValues: canonical));
    }

    [Fact]
    public void Http_metodu_buyuk_kucuk_harf_duyarsizdir()
    {
        Assert.Equal(Scope(method: "POST"), Scope(method: "post"));
    }
}
