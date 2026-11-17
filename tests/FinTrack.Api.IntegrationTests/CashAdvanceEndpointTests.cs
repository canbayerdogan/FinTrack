using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Nakit avans API sözleşmesi (docs/06 §6.5) ve Slice 6 kabul testleri
/// (docs/07 §4). Her test kendi kullanıcısını oluşturur.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CashAdvanceEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string CardsBase = "/api/v1/cards";
    private const string InflowsBase = "/api/v1/cash-inflows";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
    private static readonly DateOnly TakenOn = Today.AddDays(-2);

    [Fact]
    public async Task Avans_bagli_para_girisi_ve_bakiye_dogru()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);

        var response = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/cash-advances",
            new
            {
                amount = 3_000m,
                currency = "try",
                takenOn = TakenOn,
                description = "  Acil nakit  ",
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var advance = await response.Content.ReadFromJsonAsync<CashAdvanceResponse>();
        Assert.NotNull(advance);
        Assert.Equal($"{CardsBase}/{card.Id}/cash-advances/{advance!.Id}", response.Headers.Location?.ToString());
        Assert.Equal(card.Id, advance.CardId);
        Assert.Equal(3_000m, advance.Amount);
        Assert.Equal("TRY", advance.Currency);
        Assert.Equal(TakenOn, advance.TakenOn);
        Assert.Equal("Acil nakit", advance.Description);
        Assert.Equal("Active", advance.Status);
        Assert.NotEqual(default, advance.LinkedCashInflowId);
        Assert.Equal(8, Convert.FromBase64String(advance.RowVersion).Length);

        var inflow = await client.GetFromJsonAsync<CashInflowResponse>(
            $"{InflowsBase}/{advance.LinkedCashInflowId}");
        Assert.NotNull(inflow);
        Assert.Equal(3_000m, inflow!.Amount);
        Assert.Equal("CashAdvance", inflow.Source);
        Assert.False(inflow.IsEarnedIncome);
        Assert.Equal(advance.Id, inflow.DebtLink?.Id);
        Assert.Equal("CashAdvance", inflow.DebtLink?.Type);

        var list = await client.GetFromJsonAsync<PagedResponse<CashAdvanceResponse>>(
            $"{CardsBase}/{card.Id}/cash-advances");
        Assert.NotNull(list);
        Assert.Equal(advance.Id, Assert.Single(list!.Items).Id);

        var balance = await client.GetFromJsonAsync<CardBalanceResponse>($"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(balance);
        Assert.Equal(3_000m, balance!.Debt);
        Assert.Equal(3_000m, balance.Breakdown.CashAdvances);

        var payment = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/payments",
            new { amount = 1_000m, currency = "TRY", paidOn = TakenOn, note = (string?)null });
        Assert.Equal(HttpStatusCode.Created, payment.StatusCode);

        balance = await client.GetFromJsonAsync<CardBalanceResponse>($"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(balance);
        Assert.Equal(2_000m, balance!.Debt);
        Assert.Equal(3_000m, balance.Breakdown.CashAdvances);
        Assert.Equal(1_000m, balance.Breakdown.Payments);
    }

    [Fact]
    public async Task Iptal_bagli_para_girisini_de_kapatir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);
        var advance = await CreateAdvanceAsync(client, card.Id, 2_000m);

        var withoutReason = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/cash-advances/{advance.Id}/cancel",
            new { reason = (string?)null, rowVersion = advance.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.True(await HasFieldErrorAsync(withoutReason, "reason"));

        var cancelled = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/cash-advances/{advance.Id}/cancel",
            new { reason = "Yanlış işlem", rowVersion = advance.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        var active = await client.GetFromJsonAsync<PagedResponse<CashAdvanceResponse>>(
            $"{CardsBase}/{card.Id}/cash-advances");
        Assert.NotNull(active);
        Assert.Empty(active!.Items);

        var cancelledList = await client.GetFromJsonAsync<PagedResponse<CashAdvanceResponse>>(
            $"{CardsBase}/{card.Id}/cash-advances?status=Cancelled");
        Assert.NotNull(cancelledList);
        Assert.Equal(advance.Id, Assert.Single(cancelledList!.Items).Id);

        var inflow = await client.GetFromJsonAsync<CashInflowResponse>(
            $"{InflowsBase}/{advance.LinkedCashInflowId}");
        Assert.NotNull(inflow);
        Assert.Equal("Cancelled", inflow!.Status);

        var balance = await client.GetFromJsonAsync<CardBalanceResponse>($"{CardsBase}/{card.Id}/balance");
        Assert.NotNull(balance);
        Assert.Equal(0m, balance!.Debt);
        Assert.Equal(0m, balance.Breakdown.CashAdvances);
    }

    [Fact]
    public async Task Gecersiz_avans_yarim_kayit_olusturmaz()
    {
        using var session = await RegisterAsync();
        var client = session.Client;
        var card = await CreateCardAsync(client);

        var future = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/cash-advances",
            new { amount = 500m, currency = "TRY", takenOn = Today.AddDays(1) });
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.True(await HasFieldErrorAsync(future, "takenOn"));

        var negative = await client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/cash-advances",
            new { amount = -1m, currency = "TRY", takenOn = TakenOn });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.True(await HasFieldErrorAsync(negative, "amount"));

        var advances = await client.GetFromJsonAsync<PagedResponse<CashAdvanceResponse>>(
            $"{CardsBase}/{card.Id}/cash-advances?includeCancelled=true");
        Assert.NotNull(advances);
        Assert.Empty(advances!.Items);

        var inflows = await client.GetFromJsonAsync<PagedResponse<CashInflowResponse>>(
            $"{InflowsBase}?includeCancelled=true");
        Assert.NotNull(inflows);
        Assert.Empty(inflows!.Items);
    }

    [Fact]
    public async Task Baska_kullanicinin_karti_404_doner()
    {
        using var owner = await RegisterAsync();
        using var other = await RegisterAsync();
        var card = await CreateCardAsync(owner.Client);

        var response = await other.Client.PostAsJsonAsync(
            $"{CardsBase}/{card.Id}/cash-advances",
            new { amount = 100m, currency = "TRY", takenOn = TakenOn });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NotFound", await ReadCodeAsync(response));
    }

    private async Task<Session> RegisterAsync()
    {
        var client = fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"user-{Guid.NewGuid():N}@example.com",
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return new Session(client);
    }

    private static async Task<CardResponse> CreateCardAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(CardsBase, new
        {
            bankName = "Örnek Bank",
            displayName = "Platinum",
            logoKey = "ornek-bank",
            creditLimit = 50_000m,
            statementDay = 25,
            paymentDueDay = 5,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.NotNull(card);
        return card!;
    }

    private static async Task<CashAdvanceResponse> CreateAdvanceAsync(
        HttpClient client,
        Guid cardId,
        decimal amount)
    {
        var response = await client.PostAsJsonAsync(
            $"{CardsBase}/{cardId}/cash-advances",
            new { amount, currency = "TRY", takenOn = TakenOn, description = "test" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var advance = await response.Content.ReadFromJsonAsync<CashAdvanceResponse>();
        Assert.NotNull(advance);
        return advance!;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<bool> HasFieldErrorAsync(HttpResponseMessage response, string field)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errors", out var errors) &&
               errors.ValueKind == JsonValueKind.Object &&
               errors.TryGetProperty(field, out _);
    }

    private sealed record Session(HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthResponse(Guid UserId, string AccessToken);

    private sealed record CardResponse(Guid Id, string BankName, string DisplayName);

    private sealed record CashAdvanceResponse(
        Guid Id,
        Guid CardId,
        decimal Amount,
        string Currency,
        DateOnly TakenOn,
        string? Description,
        string Status,
        Guid? LinkedCashInflowId,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record CashInflowResponse(
        Guid Id,
        decimal Amount,
        string Currency,
        DateOnly OccurredOn,
        string Source,
        bool? IsEarnedIncome,
        string? Description,
        string Status,
        DebtLinkResponse? DebtLink,
        DateTime RecordedAtUtc,
        string RowVersion);

    private sealed record DebtLinkResponse(string Type, Guid Id);

    private sealed record CardBalanceResponse(
        Guid CardId,
        DateOnly AsOfDate,
        string Currency,
        decimal Debt,
        bool IsInCredit,
        BalanceBreakdown Breakdown);

    private sealed record BalanceBreakdown(
        decimal Expenses,
        decimal Refunds,
        decimal CashAdvances,
        decimal Payments);

    private sealed record PagedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
}
