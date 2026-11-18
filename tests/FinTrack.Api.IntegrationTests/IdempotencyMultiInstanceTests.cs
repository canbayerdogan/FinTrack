using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinTrack.Api.Idempotency;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>S4, S22, S23, S24 — UNIQUE (ScopeHash, Key) çoklu instance arbitri.</summary>
[Collection(ApiCollection.Name)]
public sealed class IdempotencyMultiInstanceTests(ApiFixture fixture) : IdempotencyTestBase(fixture)
{
    [Fact]
    public Task S4_Iki_instance_ayni_key_commit_replay() => AssertCommitBranchAsync();

    [Fact]
    public Task S22_Ilk_transaction_rollback_ikinci_instance_execution() => AssertRollbackBranchAsync();

    [Fact]
    public Task S23_Lock_timeout_ikinci_finansal_kaydi_engeller() => AssertLockTimeoutBranchAsync();

    [Fact]
    public async Task S24_Iki_instance_matrisi_tamamlanir()
    {
        await AssertCommitBranchAsync();
        await AssertRollbackBranchAsync();
        await AssertLockTimeoutBranchAsync();
    }

    private async Task AssertCommitBranchAsync()
    {
        using var secondary = Fixture.CreateFactory();
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = CreateSameUserClient(secondary, clientA.DefaultRequestHeaders.Authorization!);
        var category = await CreateCategoryAsync(clientA);
        var key = Guid.NewGuid();
        var description = $"multi-commit-{key:N}";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            BeforeCommitAsync = async () =>
            {
                started.TrySetResult();
                await release.Task;
            },
        });

        try
        {
            var taskA = PostWithKeyAsync(clientA, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var taskB = PostWithKeyAsync(clientB, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            await Task.Delay(500);
            release.TrySetResult();

            using var first = await taskA;
            using var replay = await taskB;

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
            Assert.Equal(1, await CountExpensesAsync(description));
        }
        finally
        {
            release.TrySetResult();
            Hooks.Clear(key.ToString("D"));
        }
    }

    private async Task AssertRollbackBranchAsync()
    {
        using var secondary = Fixture.CreateFactory();
        var clientA = await CreateAuthenticatedClientAsync();
        var clientB = CreateSameUserClient(secondary, clientA.DefaultRequestHeaders.Authorization!);
        var category = await CreateCategoryAsync(clientA);
        var key = Guid.NewGuid();
        var description = $"multi-rollback-{key:N}";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Hooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            BeforeCommitAsync = () =>
            {
                started.TrySetResult();
                throw new IdempotencyTestException("Test hook: ilk instance rollback.");
            },
        });

        try
        {
            var taskA = PostWithKeyAsync(clientA, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using var rolledBack = await taskA;
            using var executed = await PostWithKeyAsync(
                clientB, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);

            Assert.Equal(HttpStatusCode.Conflict, rolledBack.StatusCode);
            Assert.Equal(HttpStatusCode.Created, executed.StatusCode);
            Assert.Equal(1, await CountExpensesAsync(description));
        }
        finally
        {
            Hooks.Clear(key.ToString("D"));
        }
    }

    private async Task AssertLockTimeoutBranchAsync()
    {
        using var secondary = Fixture.CreateFactory();
        var client = await CreateAuthenticatedClientAsync(secondary);
        var category = await CreateCategoryAsync(client);
        var key = Guid.NewGuid();
        var description = $"multi-lock-{key:N}";
        var secondaryHooks = secondary.Services.GetRequiredService<IIdempotencyTestHooks>();
        secondaryHooks.Set(key.ToString("D"), new IdempotencyTestHook
        {
            ForcedInsertOutcome = IdempotencyInsertOutcome.InProgress,
        });

        try
        {
            using var response = await PostWithKeyAsync(
                client, ExpensesBase, ExpenseBody(category.Id, 10m, description), key);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("IdempotencyInProgress", await ReadCodeAsync(response));
            Assert.Equal(0, await CountExpensesAsync(description));
            Assert.Equal(0, await CountIdempotencyKeysAsync(key));
        }
        finally
        {
            secondaryHooks.Clear(key.ToString("D"));
        }
    }

    private static HttpClient CreateSameUserClient(
        WebApplicationFactory<Program> factory,
        AuthenticationHeaderValue authorization)
    {
        var client = IdempotencyTestClient.Create(factory);
        client.DefaultRequestHeaders.Authorization = authorization;
        return client;
    }

    private static object ExpenseBody(Guid categoryId, decimal amount, string description) => new
    {
        amount,
        currency = "TRY",
        occurredOn = Past,
        categoryId,
        description,
        paymentMethod = "Cash",
        cardId = (Guid?)null,
    };
}
