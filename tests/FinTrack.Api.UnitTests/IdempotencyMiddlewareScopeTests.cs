using System.Reflection;
using FinTrack.Api.Idempotency;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FinTrack.Api.UnitTests;

/// <summary>
/// docs/11 §1.1 / R6: middleware'ın pozitif allowlist kapsama kararının çalışma
/// zamanı davranışı. IdempotencyMiddleware internal olduğundan yansıma ile
/// erişilir; host kaynağı değiştirilmez ve Docker gerekmez.
/// </summary>
public sealed class IdempotencyMiddlewareScopeTests
{
    private static readonly MethodInfo IsInScope = typeof(IdempotencyScope).Assembly
        .GetType("FinTrack.Api.Idempotency.IdempotencyMiddleware", throwOnError: true)!
        .GetMethod("IsInScope", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData("POST", "/api/v1/expenses", true)]
    [InlineData("POST", "/api/v1/categories/abc/archive", true)]
    [InlineData("POST", "/api/v1/cash-inflows", true)]
    [InlineData("POST", "/api/v1/cards/abc/payments", true)]
    [InlineData("POST", "/api/v1/personal-debts/abc/repayments", true)]
    [InlineData("POST", "/api/v1/loans/abc/installments/2/pay", true)]
    [InlineData("POST", "/API/V1/EXPENSES", true)] // middleware ordinal-ignore-case kullanır
    [InlineData("POST", "/api/v1/auth/login", false)]
    [InlineData("POST", "/api/v1/reports/generate", false)]
    [InlineData("POST", "/api/v1/idempotency/abc", false)]
    [InlineData("GET", "/api/v1/expenses", false)]
    [InlineData("PUT", "/api/v1/expenses/abc", false)]
    public void Kapsam_karari_method_ve_yola_gore_verilir(string method, string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        var actual = (bool)IsInScope.Invoke(null, [context])!;

        Assert.Equal(expected, actual);
    }
}
