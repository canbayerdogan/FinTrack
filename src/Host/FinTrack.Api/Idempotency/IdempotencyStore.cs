using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Idempotency;

internal sealed class IdempotencyStore(IdempotencyDbContext db) : IIdempotencyStore
{
    public Task<IdempotencyKeyRecord?> FindAsync(
        string scopeHash,
        string key,
        CancellationToken cancellationToken) =>
        db.IdempotencyKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.ScopeHash == scopeHash && record.Key == key,
                cancellationToken);

    public async Task<IdempotencyInsertOutcome> InsertAsync(
        IdempotencyKeyRecord record,
        CancellationToken cancellationToken)
    {
        db.IdempotencyKeys.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return IdempotencyInsertOutcome.Created;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Çoklu instance arbitri: UNIQUE (ScopeHash, Key). Host transaction'ı
            // açık kalır; middleware satırı yeniden okuyup replay/key reused kararı verir.
            db.Entry(record).State = EntityState.Detached;
            return IdempotencyInsertOutcome.Duplicate;
        }
        catch (DbUpdateException exception) when (IsLockTimeout(exception))
        {
            // Başka instance anahtarı kilitledi: ikinci finansal kayıt oluşmaz.
            db.Entry(record).State = EntityState.Detached;
            return IdempotencyInsertOutcome.InProgress;
        }
    }

    public Task CompleteAsync(
        string scopeHash,
        string key,
        IdempotencyResponseMetadata response,
        DateTime completedAtUtc,
        CancellationToken cancellationToken) =>
        db.IdempotencyKeys
            .Where(record => record.ScopeHash == scopeHash && record.Key == key)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.Status, IdempotencyStatus.Completed)
                    .SetProperty(record => record.ResponseStatusCode, response.StatusCode)
                    .SetProperty(record => record.ResponseContentType, response.ContentType)
                    .SetProperty(
                        record => record.ResponseHeaders,
                        response.Location is null ? null : $"Location:{response.Location}")
                    .SetProperty(record => record.ResponseBody, response.Body)
                    .SetProperty(record => record.CompletedAtUtc, completedAtUtc),
                cancellationToken);

    public Task TombstoneAsync(
        string scopeHash,
        string key,
        CancellationToken cancellationToken) =>
        db.IdempotencyKeys
            .Where(record => record.ScopeHash == scopeHash && record.Key == key)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.Status, IdempotencyStatus.Tombstone)
                    .SetProperty(record => record.ResponseStatusCode, (int?)null)
                    .SetProperty(record => record.ResponseContentType, (string?)null)
                    .SetProperty(record => record.ResponseHeaders, (string?)null)
                    .SetProperty(record => record.ResponseBody, (string?)null),
                cancellationToken);

    public async Task<IdempotencyStatusResult?> GetStatusAsync(
        string userId,
        string key,
        CancellationToken cancellationToken)
    {
        var userHash = IdempotencyScope.UserHash(userId);
        var record = await db.IdempotencyKeys
            .AsNoTracking()
            .Where(item => item.UserHash == userHash && item.Key == key)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null
            ? null
            : new IdempotencyStatusResult(
                record.Status,
                record.CompletedAtUtc,
                record.ResponseBody is not null,
                record.ExpiresAtUtc);
    }

    internal static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } ||
        (exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ?? false);

    internal static bool IsLockTimeout(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 1222 or -2 } ||
        (exception.InnerException?.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);
}
