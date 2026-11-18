using System.Data;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Idempotency;

/// <summary> İstek dışında, saatte bir ve en fazla 1000 satır sınırlı temizlik (docs/11 §6). </summary>
internal interface IIdempotencyCleanup
{
    Task RunIfDueAsync(CancellationToken cancellationToken);

    /// <summary>Testlerde saatte bir çalışma penceresini sıfırlar.</summary>
    void ResetForTests();
}

internal sealed class IdempotencyCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<IdempotencyCleanupService> logger) : IIdempotencyCleanup
{
    private const int MaxRowsPerRun = 1000;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRunUtc = DateTime.MinValue;

    public void ResetForTests() => _lastRunUtc = DateTime.MinValue;

    public async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastRunUtc < Interval)
        {
            return;
        }

        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (DateTime.UtcNow - _lastRunUtc < Interval)
            {
                return;
            }

            // Yeni DI scope -> kendi fiziksel bağlantısı; istek transaction'ının dışında.
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            var now = DateTime.UtcNow;

            var tombstoned = await db.Database.ExecuteSqlRawAsync(
                """
                ;WITH expired AS (
                    SELECT TOP (1000) k.Id
                    FROM shared.IdempotencyKeys AS k
                    WHERE k.Status = 2 AND k.ExpiresAtUtc < {0}
                )
                UPDATE k
                SET k.Status = 3,
                    k.ResponseStatusCode = NULL,
                    k.ResponseContentType = NULL,
                    k.ResponseHeaders = NULL,
                    k.ResponseBody = NULL
                FROM shared.IdempotencyKeys AS k
                INNER JOIN expired ON expired.Id = k.Id
                """,
                new object[] { now },
                cancellationToken);

            var deleted = await db.Database.ExecuteSqlRawAsync(
                """
                ;WITH stale AS (
                    SELECT TOP (1000) k.Id
                    FROM shared.IdempotencyKeys AS k
                    WHERE k.Status = 3 AND k.TombstoneExpiresAtUtc < {0}
                )
                DELETE k
                FROM shared.IdempotencyKeys AS k
                INNER JOIN stale ON stale.Id = k.Id
                """,
                new object[] { now },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _lastRunUtc = DateTime.UtcNow;

            if (tombstoned > 0 || deleted > 0)
            {
                logger.LogInformation(
                    "Idempotency temizliği: {Tombstoned} kayıt tombstone'a çevrildi, {Deleted} kayıt silindi.",
                    tombstoned,
                    deleted);
            }
        }
        catch (Exception exception)
        {
            // Temizlik hatası isteği bloklamaz (docs/11 §6).
            logger.LogWarning(exception, "Idempotency temizliği başarısız; istek devam ediyor.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
