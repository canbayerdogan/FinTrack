using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Api.Idempotency;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>Idempotency senaryolarının ortak kurulumu ve veritabanı doğrulamaları.</summary>
public abstract class IdempotencyTestBase
{
    protected const string CategoriesBase = "/api/v1/categories";
    protected const string ExpensesBase = "/api/v1/expenses";
    protected const string InflowsBase = "/api/v1/cash-inflows";
    protected const string CardsBase = "/api/v1/cards";
    protected const string DebtsBase = "/api/v1/personal-debts";
    protected const string LoansBase = "/api/v1/loans";
    protected static readonly DateOnly Past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    protected IdempotencyTestBase(ApiFixture fixture) => Fixture = fixture;

    protected ApiFixture Fixture { get; }

    protected WebApplicationFactory<Program> Factory => Fixture.Factory;

    internal IIdempotencyTestHooks Hooks => Factory.Services.GetRequiredService<IIdempotencyTestHooks>();

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program>? factory = null,
        bool addIdempotencyHeaders = true)
    {
        var target = factory ?? Factory;
        var client = addIdempotencyHeaders
            ? IdempotencyTestClient.Create(target)
            : IdempotencyTestClient.CreatePlain(target);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"idem-{Guid.NewGuid():N}@example.com",
            password = "Test12345",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    protected static async Task<HttpResponseMessage> PostWithKeyAsync(
        HttpClient client,
        string url,
        object body,
        Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request);
    }

    protected static async Task<HttpResponseMessage> PostRawWithKeyAsync(
        HttpClient client,
        string url,
        string json,
        Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request);
    }

    protected static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value!;
    }

    protected async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name = "Market")
    {
        var response = await client.PostAsJsonAsync(CategoriesBase, new { name, iconKey = "cart" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CategoryDto>(response);
    }

    protected async Task<ExpenseDto> CreateExpenseAsync(
        HttpClient client,
        Guid categoryId,
        decimal amount = 100m,
        string description = "idempotency-test")
    {
        var response = await client.PostAsJsonAsync(ExpensesBase, new
        {
            amount,
            currency = "TRY",
            occurredOn = Past,
            categoryId,
            description,
            paymentMethod = "Cash",
            cardId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ExpenseDto>(response);
    }

    protected async Task<CardDto> CreateCardAsync(HttpClient client)
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
        return await ReadAsync<CardDto>(response);
    }

    protected async Task<long> CountAsync(string query, params object[] parameters)
    {
        await using var connection = new SqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        for (var i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    protected async Task<int> ExecuteAsync(string commandText, params object[] parameters)
    {
        await using var connection = new SqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        for (var i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    protected Task<long> CountExpensesAsync(string description) =>
        CountAsync("SELECT COUNT(*) FROM expenses.Expenses WHERE Description = @p0", description);

    protected Task<long> CountCategoriesAsync(string name) =>
        CountAsync("SELECT COUNT(*) FROM expenses.Categories WHERE Name = @p0", name);

    protected Task<long> CountInflowsAsync(string description) =>
        CountAsync("SELECT COUNT(*) FROM inflows.CashInflows WHERE Description = @p0", description);

    protected Task<long> CountIdempotencyKeysAsync(Guid key) =>
        CountAsync("SELECT COUNT(*) FROM shared.IdempotencyKeys WHERE [Key] = @p0", key.ToString("D"));

    protected sealed record AuthResponse(Guid UserId, string AccessToken);

    protected sealed record CategoryDto(Guid Id, string Name, string? IconKey, bool IsArchived, string RowVersion);

    protected sealed record ExpenseDto(
        Guid Id,
        decimal Amount,
        string Currency,
        DateOnly OccurredOn,
        Guid CategoryId,
        string? Description,
        string Status,
        string RowVersion);

    protected sealed record CardDto(
        Guid Id,
        string BankName,
        string DisplayName,
        decimal? CreditLimit,
        string RowVersion);
}
