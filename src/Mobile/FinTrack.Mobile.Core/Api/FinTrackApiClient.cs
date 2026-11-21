using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// API v1 için tek typed client. Tüm yollar /api/v1 base address üzerine
/// göreli olarak kurulur; hata yanıtları ApiException'a dönüştürülür.
/// </summary>
public sealed class FinTrackApiClient
{
    private readonly HttpClient _http;

    public FinTrackApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<AuthResponse> LoginAsync(string email, string password, CancellationToken ct = default) =>
        PostAsync<AuthResponse>("auth/login", new LoginRequest(email, password), ct);

    public Task<AuthResponse> RegisterAsync(
        string email,
        string password,
        string preferredTimeZone,
        CancellationToken ct = default) =>
        PostAsync<AuthResponse>(
            "auth/register",
            new RegisterRequest(email, password, preferredTimeZone),
            ct);

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "auth/logout",
            new RefreshRequest(refreshToken),
            ApiJson.Options,
            ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<ProfileResponse> GetProfileAsync(CancellationToken ct = default) =>
        GetAsync<ProfileResponse>("auth/me", ct);

    public Task<ProfileResponse> UpdateProfileAsync(
        string preferredTimeZone,
        string rowVersion,
        CancellationToken ct = default) =>
        PutAsync<ProfileResponse>(
            "auth/me",
            new UpdateProfileRequest(preferredTimeZone, rowVersion),
            ct);

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CategoryDto>>("categories", ct);

    public Task<PagedResult<ExpenseDto>> GetExpensesAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? categoryId = null,
        Guid? cardId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new StringBuilder("expenses?");
        AppendDate(query, "from", from);
        AppendDate(query, "to", to);
        AppendGuid(query, "categoryId", categoryId);
        AppendGuid(query, "cardId", cardId);
        AppendNumber(query, "page", page);
        AppendNumber(query, "pageSize", pageSize);
        return GetAsync<PagedResult<ExpenseDto>>(query.ToString(), ct);
    }

    public Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken ct = default) =>
        PostAsync<ExpenseDto>("expenses", request, ct);

    public Task<IReadOnlyList<CardDto>> GetCardsAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CardDto>>("cards", ct);

    public Task<CardBalanceDto> GetCardBalanceAsync(
        Guid cardId,
        DateOnly? asOf = null,
        CancellationToken ct = default)
    {
        var url = $"cards/{cardId:D}/balance";
        if (asOf.HasValue)
        {
            url += $"?asOf={asOf.Value:yyyy-MM-dd}";
        }

        return GetAsync<CardBalanceDto>(url, ct);
    }

    public Task<PagedResult<StatementDto>> GetStatementsAsync(Guid cardId, CancellationToken ct = default) =>
        GetAsync<PagedResult<StatementDto>>($"cards/{cardId:D}/statements?pageSize=50", ct);

    public Task<PagedResult<LoanDto>> GetLoansAsync(CancellationToken ct = default) =>
        GetAsync<PagedResult<LoanDto>>("loans?pageSize=50", ct);

    public Task<PagedResult<PersonalDebtDto>> GetPersonalDebtsAsync(CancellationToken ct = default) =>
        GetAsync<PagedResult<PersonalDebtDto>>("personal-debts?pageSize=50", ct);

    public Task<MonthlyReport> GetMonthlyReportAsync(int year, int month, CancellationToken ct = default) =>
        GetAsync<MonthlyReport>($"reports/monthly?year={year}&month={month}", ct);

    public Task<DebtSummary> GetDebtSummaryAsync(CancellationToken ct = default) =>
        GetAsync<DebtSummary>("reports/debt-summary", ct);

    public Task<UpcomingPayments> GetUpcomingPaymentsAsync(int days = 30, CancellationToken ct = default) =>
        GetAsync<UpcomingPayments>($"reports/upcoming-payments?days={days}", ct);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync<T>(request, ct);
    }

    private async Task<T> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: ApiJson.Options),
        };
        return await SendAsync<T>(request, ct);
    }

    private async Task<T> PutAsync<T>(string url, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body, options: ApiJson.Options),
        };
        return await SendAsync<T>(request, ct);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default!;
        }

        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct)
               ?? throw new InvalidOperationException("API boş bir yanıt döndürdü.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await ApiException.FromResponseAsync(response, ct);
        }
    }

    private static void AppendDate(StringBuilder query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            Append(query, name, value.Value.ToString("yyyy-MM-dd"));
        }
    }

    private static void AppendGuid(StringBuilder query, string name, Guid? value)
    {
        if (value.HasValue)
        {
            Append(query, name, value.Value.ToString("D"));
        }
    }

    private static void AppendNumber(StringBuilder query, string name, int value) =>
        Append(query, name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void Append(StringBuilder query, string name, string value)
    {
        if (query.Length > 0 && query[^1] != '?' && query[^1] != '&')
        {
            query.Append('&');
        }

        query.Append(name).Append('=').Append(Uri.EscapeDataString(value));
    }

    private sealed record LoginRequest(string Email, string Password);

    private sealed record RegisterRequest(string Email, string Password, string PreferredTimeZone);

    private sealed record RefreshRequest(string RefreshToken);

    private sealed record UpdateProfileRequest(string PreferredTimeZone, string RowVersion);
}

/// <summary>
/// POST /expenses gövdesi. Kart alışverişi de bu kayıt üzerinden girilir
/// (ADR-0008); cardId yalnızca CreditCard için gönderilir.
/// </summary>
public sealed record CreateExpenseRequest(
    decimal Amount,
    DateOnly OccurredOn,
    Guid CategoryId,
    string PaymentMethod,
    Guid? CardId,
    string? Description,
    string Currency = "TRY");

/// <summary>
/// POST /cards gövdesi. Yalnızca banka/kart adı ve isteğe bağlı bilgiler
/// gönderilir; kart numarası, CVV veya son kullanma tarihi yoktur.
/// </summary>
public sealed record CreateCardRequest(
    string BankName,
    string DisplayName,
    decimal? CreditLimit,
    int? StatementDay,
    int? PaymentDueDay);
