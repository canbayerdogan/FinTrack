using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Para girişi sözleşmesi (docs/06 §5) ve docs/07 slice 5 kabul testleri.
/// Her test kendi kullanıcısını üretir; testler sıraya bağımlı değildir.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CashInflowEndpointTests(ApiFixture fixture)
{
    private const string Password = "GucluParola1";
    private const string Base = "/api/v1/cash-inflows";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    [Fact]
    public async Task Olusturma_getirme_guncelleme_ve_iptal()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var create = await client.PostAsJsonAsync(Base, new
        {
            amount = 80_000m,
            currency = "try",
            occurredOn = Today.AddDays(-2),
            source = "Salary",
            description = "  Kasım maaşı  ",
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var inflow = await create.Content.ReadFromJsonAsync<CashInflowResponse>();
        Assert.NotNull(inflow);
        Assert.Equal($"{Base}/{inflow!.Id}", create.Headers.Location?.ToString());
        Assert.Equal(80_000m, inflow.Amount);
        Assert.Equal("TRY", inflow.Currency);
        Assert.Equal(Today.AddDays(-2), inflow.OccurredOn);
        Assert.Equal("Salary", inflow.Source);
        Assert.True(inflow.IsEarnedIncome);
        Assert.Equal("Kasım maaşı", inflow.Description);
        Assert.Equal("Active", inflow.Status);
        Assert.Null(inflow.DebtLink);
        Assert.NotEqual(default, inflow.RecordedAtUtc);
        Assert.Equal(8, Convert.FromBase64String(inflow.RowVersion).Length);

        var fetched = await client.GetFromJsonAsync<CashInflowResponse>($"{Base}/{inflow.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(inflow.RowVersion, fetched!.RowVersion);

        var recordedAtRejected = await client.PutAsJsonAsync($"{Base}/{inflow.Id}", new
        {
            amount = 85_000m,
            currency = "TRY",
            occurredOn = Today.AddDays(-2),
            source = "Salary",
            description = "yeni",
            rowVersion = inflow.RowVersion,
            recordedAtUtc = DateTime.UtcNow,
        });
        Assert.Equal(HttpStatusCode.BadRequest, recordedAtRejected.StatusCode);
        Assert.True(await HasFieldErrorAsync(recordedAtRejected, "recordedAtUtc"));

        var derivedRejected = await client.PutAsJsonAsync($"{Base}/{inflow.Id}", new
        {
            amount = 85_000m,
            currency = "TRY",
            occurredOn = Today.AddDays(-2),
            source = "Salary",
            description = "yeni",
            rowVersion = inflow.RowVersion,
            isEarnedIncome = false,
        });
        Assert.Equal(HttpStatusCode.BadRequest, derivedRejected.StatusCode);
        Assert.True(await HasFieldErrorAsync(derivedRejected, "isEarnedIncome"));

        var update = await client.PutAsJsonAsync($"{Base}/{inflow.Id}", new
        {
            amount = 90_000m,
            currency = "TRY",
            occurredOn = Today.AddDays(-1),
            source = "BusinessIncome",
            description = "Güncel",
            rowVersion = inflow.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<CashInflowResponse>();
        Assert.NotNull(updated);
        Assert.Equal(90_000m, updated!.Amount);
        Assert.Equal("BusinessIncome", updated.Source);
        Assert.True(updated.IsEarnedIncome);
        Assert.NotEqual(inflow.RowVersion, updated.RowVersion);

        var stale = await client.PutAsJsonAsync($"{Base}/{inflow.Id}", new
        {
            amount = 95_000m,
            currency = "TRY",
            occurredOn = Today.AddDays(-1),
            source = "Salary",
            description = "stale",
            rowVersion = inflow.RowVersion,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(stale));

        var withoutReason = await client.PostAsJsonAsync(
            $"{Base}/{inflow.Id}/cancel",
            new { reason = (string?)null, rowVersion = updated.RowVersion });
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.True(await HasFieldErrorAsync(withoutReason, "reason"));

        var staleCancel = await client.PostAsJsonAsync(
            $"{Base}/{inflow.Id}/cancel",
            new
            {
                reason = "İptal",
                rowVersion = Convert.ToBase64String(new byte[8]),
            });
        Assert.Equal(HttpStatusCode.Conflict, staleCancel.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(staleCancel));

        var cancel = await client.PostAsJsonAsync(
            $"{Base}/{inflow.Id}/cancel",
            new { reason = "Yanlış kayıt", rowVersion = updated.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var cancelled = await client.GetFromJsonAsync<CashInflowResponse>($"{Base}/{inflow.Id}");
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);

        var cancelAgain = await client.PostAsJsonAsync(
            $"{Base}/{inflow.Id}/cancel",
            new { reason = "Tekrar", rowVersion = cancelled.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, cancelAgain.StatusCode);

        var defaultList = await client.GetFromJsonAsync<PagedResponse>(Base);
        Assert.NotNull(defaultList);
        Assert.Equal(0, defaultList!.TotalCount);

        var withCancelled = await client.GetFromJsonAsync<PagedResponse>($"{Base}?includeCancelled=true");
        Assert.NotNull(withCancelled);
        Assert.Equal(1, withCancelled!.TotalCount);

        var cancelledOnly = await client.GetFromJsonAsync<PagedResponse>($"{Base}?status=Cancelled");
        Assert.NotNull(cancelledOnly);
        Assert.Equal(1, cancelledOnly!.TotalCount);

        var activeOnly = await client.GetFromJsonAsync<PagedResponse>($"{Base}?status=Active");
        Assert.NotNull(activeOnly);
        Assert.Equal(0, activeOnly!.TotalCount);
    }

    [Fact]
    public async Task Kaynaktan_turetilen_gelir_niteligi_ve_borc_kaynaklari()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var salary = await CreateAsync(client, 1_000m, CashInflowSourceName.Salary);
        Assert.True(salary.IsEarnedIncome);
        Assert.Null(salary.DebtLink);

        var business = await CreateAsync(client, 1_000m, CashInflowSourceName.BusinessIncome);
        Assert.True(business.IsEarnedIncome);

        var assetSale = await CreateAsync(client, 1_000m, CashInflowSourceName.AssetSale);
        Assert.False(assetSale.IsEarnedIncome);
        Assert.Null(assetSale.DebtLink);

        var unknown = await CreateAsync(client, 1_000m, CashInflowSourceName.Unknown);
        Assert.Null(unknown.IsEarnedIncome);

        var derived = await client.PostAsJsonAsync(Base, new
        {
            amount = 1_000m,
            currency = "TRY",
            occurredOn = Today,
            source = "Salary",
            isEarnedIncome = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, derived.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(derived));
        Assert.True(await HasFieldErrorAsync(derived, "isEarnedIncome"));

        foreach (var source in new[]
                 {
                     CashInflowSourceName.BankLoan,
                     CashInflowSourceName.CashAdvance,
                     CashInflowSourceName.PersonalBorrowing,
                 })
        {
            var debtSource = await client.PostAsJsonAsync(Base, new
            {
                amount = 1_000m,
                currency = "TRY",
                occurredOn = Today,
                source,
            });
            Assert.Equal(HttpStatusCode.BadRequest, debtSource.StatusCode);
            Assert.Equal("DebtSourceRequiresOwnerEndpoint", await ReadCodeAsync(debtSource));
            Assert.True(await HasFieldErrorAsync(debtSource, "source"));
        }

        var invalidSource = await client.PostAsJsonAsync(Base, new
        {
            amount = 1_000m,
            currency = "TRY",
            occurredOn = Today,
            source = "Kripto",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSource.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(invalidSource));
        Assert.True(await HasFieldErrorAsync(invalidSource, "source"));

        var missingSource = await client.PostAsJsonAsync(Base, new
        {
            amount = 1_000m,
            currency = "TRY",
            occurredOn = Today,
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingSource.StatusCode);
        Assert.True(await HasFieldErrorAsync(missingSource, "source"));
    }

    [Fact]
    public async Task Liste_filtreleri_ve_sayfalama_calisir()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        await CreateAsync(client, 10m, CashInflowSourceName.Salary, Today.AddDays(-3));
        await CreateAsync(client, 20m, CashInflowSourceName.AssetSale, Today.AddDays(-2));
        await CreateAsync(client, 30m, CashInflowSourceName.BusinessIncome, Today.AddDays(-1));

        var all = await client.GetFromJsonAsync<PagedResponse>(Base);
        Assert.NotNull(all);
        Assert.Equal(3, all!.TotalCount);
        Assert.Equal(1, all.TotalPages);
        Assert.Equal(Today.AddDays(-1), all.Items[0].OccurredOn);

        var from = await client.GetFromJsonAsync<PagedResponse>($"{Base}?from={Today.AddDays(-2):yyyy-MM-dd}");
        Assert.NotNull(from);
        Assert.Equal(2, from!.TotalCount);

        var to = await client.GetFromJsonAsync<PagedResponse>($"{Base}?to={Today.AddDays(-3):yyyy-MM-dd}");
        Assert.NotNull(to);
        Assert.Equal(1, to!.TotalCount);
        Assert.Equal("Salary", to.Items[0].Source);

        var bySource = await client.GetFromJsonAsync<PagedResponse>($"{Base}?source=AssetSale");
        Assert.NotNull(bySource);
        Assert.Equal(1, bySource!.TotalCount);
        Assert.Equal("AssetSale", bySource.Items[0].Source);

        var secondPage = await client.GetFromJsonAsync<PagedResponse>($"{Base}?page=2&pageSize=1");
        Assert.NotNull(secondPage);
        Assert.Equal(3, secondPage!.TotalCount);
        Assert.Equal(3, secondPage.TotalPages);
        Assert.Equal("AssetSale", Assert.Single(secondPage.Items).Source);

        await AssertValidationFieldAsync(client, $"{Base}?source=Kripto", "source");
        await AssertValidationFieldAsync(client, $"{Base}?status=Bilinmeyen", "status");
        await AssertValidationFieldAsync(client, $"{Base}?page=0", "page");
        await AssertValidationFieldAsync(client, $"{Base}?pageSize=201", "pageSize");
        await AssertValidationFieldAsync(
            client,
            $"{Base}?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today.AddDays(-2):yyyy-MM-dd}",
            "from");
        await AssertValidationFieldAsync(client, $"{Base}?from=2026-13-40", "from");
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_kaydina_erisemez()
    {
        using var userA = await RegisterAsync();
        using var userB = await RegisterAsync();

        var inflow = await CreateAsync(userA.Client, 100m, CashInflowSourceName.Salary);

        var otherGet = await userB.Client.GetAsync($"{Base}/{inflow.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherGet.StatusCode);

        var otherList = await userB.Client.GetFromJsonAsync<PagedResponse>($"{Base}?includeCancelled=true");
        Assert.NotNull(otherList);
        Assert.Equal(0, otherList!.TotalCount);

        var otherUpdate = await userB.Client.PutAsJsonAsync($"{Base}/{inflow.Id}", new
        {
            amount = 1m,
            currency = "TRY",
            occurredOn = Today,
            source = "Salary",
            rowVersion = inflow.RowVersion,
        });
        Assert.Equal(HttpStatusCode.NotFound, otherUpdate.StatusCode);

        var otherCancel = await userB.Client.PostAsJsonAsync(
            $"{Base}/{inflow.Id}/cancel",
            new { reason = "İptal", rowVersion = inflow.RowVersion });
        Assert.Equal(HttpStatusCode.NotFound, otherCancel.StatusCode);
    }

    [Fact]
    public async Task OpenApi_para_girisi_uc_noktalarini_icerir_ve_status_kaldirildi()
    {
        using var session = await RegisterAsync();
        var client = session.Client;

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/api/v1/cash-inflows", document);
        Assert.Contains("/api/v1/cash-inflows/{id}", document);
        Assert.Contains("/api/v1/cash-inflows/{id}/cancel", document);
        Assert.DoesNotContain("/api/v1/cash-inflows/_status", document);

        var status = await client.GetAsync($"{Base}/_status");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
    }

    private async Task<Session> RegisterAsync()
    {
        var client = IdempotencyTestClient.Create(fixture.Factory);
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

    private static async Task<CashInflowResponse> CreateAsync(
        HttpClient client,
        decimal amount,
        string source,
        DateOnly? occurredOn = null,
        string? description = null)
    {
        var response = await client.PostAsJsonAsync(Base, new
        {
            amount,
            currency = "TRY",
            occurredOn = occurredOn ?? Today,
            source,
            description,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var inflow = await response.Content.ReadFromJsonAsync<CashInflowResponse>();
        Assert.NotNull(inflow);
        return inflow!;
    }

    private static async Task AssertValidationFieldAsync(HttpClient client, string url, string field)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, field));
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

    private static class CashInflowSourceName
    {
        public const string Salary = "Salary";
        public const string BusinessIncome = "BusinessIncome";
        public const string BankLoan = "BankLoan";
        public const string CashAdvance = "CashAdvance";
        public const string PersonalBorrowing = "PersonalBorrowing";
        public const string AssetSale = "AssetSale";
        public const string Unknown = "Unknown";
    }

    private sealed record Session(HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record AuthResponse(Guid UserId, string AccessToken);

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

    private sealed record PagedResponse(
        List<CashInflowResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
