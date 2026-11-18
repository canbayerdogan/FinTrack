using System.Collections.Concurrent;
using System.Data.Common;
using System.Text;
using FinTrack.Api.WriteTransactions;
using FinTrack.ModuleContracts;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Api.Idempotency;

/// <summary>
/// Mutasyon POST'larında tek transaction, tek commit ve 2xx replay sağlar (docs/11 §4, ADR-0010).
/// </summary>
internal sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private const int MaxBodyBytes = 64 * 1024;
    private const int MaxResponseBytes = 64 * 1024;

    /// <summary> docs/11 §1.1 pozitif listesi. Yeni mutasyon POST eklenirse burası güncellenir (R6). </summary>
    private static readonly string[] InScopePrefixes =
    [
        "/api/v1/expenses",
        "/api/v1/categories",
        "/api/v1/cash-inflows",
        "/api/v1/cards",
        "/api/v1/personal-debts",
        "/api/v1/loans",
    ];

    /// <summary> Aynı app instance'ında eşzamanlı aynı anahtar; W5. </summary>
    private readonly ConcurrentDictionary<string, byte> _activeKeys = new(StringComparer.Ordinal);

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser currentUser,
        IIdempotencyStore store,
        IWriteTransactionScopeFactory transactionFactory,
        IWriteTransactionScopeSetter scopeSetter,
        IdempotencyDbContext idempotencyDb,
        IEnumerable<IWriteScopeParticipant> participants,
        IIdempotencyCleanup cleanup,
        IIdempotencyTestHooks testHooks)
    {
        if (!IsInScope(context))
        {
            await next(context);
            return;
        }

        var rawKey = context.Request.Headers[IdempotencyScope.HeaderName];
        if (rawKey.Count != 1 || string.IsNullOrWhiteSpace(rawKey[0]) ||
            !Guid.TryParse(rawKey[0], out var parsedKey))
        {
            await IdempotencyProblems.Validation(
                context, "idempotencyKey", "Idempotency-Key geçerli bir UUID olmalıdır.").ExecuteAsync(context);
            return;
        }

        var key = parsedKey.ToString("D");

        var contentType = context.Request.ContentType;
        if (contentType is null ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            await IdempotencyProblems.Validation(
                context, "body", "İstek gövdesi application/json olmalıdır.").ExecuteAsync(context);
            return;
        }

        Guid userId;
        try
        {
            userId = currentUser.RequireUserId();
        }
        catch (UnauthorizedAccessException)
        {
            await next(context);
            return;
        }

        string body;
        try
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(
                context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024, leaveOpen: true);
            body = await reader.ReadToEndAsync(context.RequestAborted);
            context.Request.Body.Position = 0;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Idempotency istek gövdesi okunamadı; key={Key}", key);
            await IdempotencyProblems.Validation(
                context, "body", "İstek gövdesi okunamadı.").ExecuteAsync(context);
            return;
        }

        if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
        {
            await IdempotencyProblems.Validation(
                context, "body", "İstek gövdesi 64 KB sınırını aştı.").ExecuteAsync(context);
            return;
        }

        string requestHash;
        try
        {
            requestHash = IdempotencyCanonicalizer.ComputeRequestHash(body);
        }
        catch (IdempotencyCanonicalizer.InvalidBodyException exception)
        {
            await IdempotencyProblems.Validation(context, "body", exception.Message).ExecuteAsync(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var routeTemplate = endpoint is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText ?? context.Request.Path.Value ?? string.Empty
            : context.Request.Path.Value ?? string.Empty;
        var routeValues = IdempotencyScope.CanonicalRouteValues(
            context.Request.RouteValues.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString(),
                StringComparer.Ordinal));

        var scopeHash = IdempotencyScope.Compute(
            userId.ToString(), "POST", routeTemplate, routeValues, key);

        // Kısa temizlik istek transaction'ı açılmadan önce, ayrı bağlantıda çalışır.
        await cleanup.RunIfDueAsync(context.RequestAborted);

        var activeKey = scopeHash + "\n" + key;
        if (!_activeKeys.TryAdd(activeKey, 0))
        {
            await IdempotencyProblems.InProgress(context).ExecuteAsync(context);
            return;
        }

        var enlistedParticipants = participants.ToArray();
        IWriteTransactionScope? scope = null;
        var committed = false;
        var originalBody = context.Response.Body;
        MemoryStream? buffer = null;

        try
        {
            scope = await transactionFactory.BeginAsync(context.RequestAborted);
            scopeSetter.Set(scope);
            idempotencyDb.Database.UseTransaction(scope.Transaction);
            foreach (var participant in enlistedParticipants)
            {
                participant.Enlist(scope);
            }

            var existing = await store.FindAsync(scopeHash, key, context.RequestAborted);
            if (existing is not null)
            {
                committed = await HandleExistingAsync(
                    context, store, scope, existing, scopeHash, key, requestHash, context.RequestAborted);
                return;
            }

            var now = DateTime.UtcNow;
            var record = new IdempotencyKeyRecord
            {
                ScopeHash = scopeHash,
                UserHash = IdempotencyScope.UserHash(userId.ToString()),
                Key = key,
                RequestHash = requestHash,
                Status = IdempotencyStatus.InProgress,
                RequestContentType = contentType,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddHours(24),
                TombstoneExpiresAtUtc = now.AddDays(30),
            };

            var hook = testHooks.Get(key);
            var inserted = hook?.ForcedInsertOutcome ??
                           await store.InsertAsync(record, context.RequestAborted);
            if (inserted != IdempotencyInsertOutcome.Created)
            {
                if (inserted == IdempotencyInsertOutcome.InProgress)
                {
                    await IdempotencyProblems.InProgress(context).ExecuteAsync(context);
                    return;
                }

                // UNIQUE ihlali: başka instance kazandı; satırı yeniden oku.
                existing = await store.FindAsync(scopeHash, key, context.RequestAborted);
                if (existing is null)
                {
                    await IdempotencyProblems.InProgress(context).ExecuteAsync(context);
                    return;
                }

                committed = await HandleExistingAsync(
                    context, store, scope, existing, scopeHash, key, requestHash, context.RequestAborted);
                return;
            }

            buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await next(context);
            }
            finally
            {
                context.Response.Body = originalBody;
            }

            var statusCode = context.Response.StatusCode;
            var responseBytes = hook?.ResponseBodyOverride is { } overrideBody
                ? Encoding.UTF8.GetBytes(overrideBody)
                : buffer.ToArray();
            var responseContentType = context.Response.ContentType ?? "application/json; charset=utf-8";
            var location = context.Response.Headers.Location.FirstOrDefault();
            if (string.IsNullOrEmpty(location))
            {
                location = null;
            }

            if (statusCode is >= StatusCodes.Status200OK and <= 299)
            {
                if (responseBytes.Length > MaxResponseBytes)
                {
                    logger.LogError(
                        "Idempotency yanıtı 64 KB sınırını aştı; key={Key}, status={Status}", key, statusCode);
                    await IdempotencyProblems.ResponseTooLarge(context).ExecuteAsync(context);
                    return;
                }

                try
                {
                    await store.CompleteAsync(
                        scopeHash,
                        key,
                        new IdempotencyResponseMetadata(
                            statusCode,
                            responseContentType,
                            location,
                            Encoding.UTF8.GetString(responseBytes)),
                        DateTime.UtcNow,
                        context.RequestAborted);
                    if (hook?.BeforeCommitAsync is not null)
                    {
                        await hook.BeforeCommitAsync();
                    }

                    if (hook?.CommitMode is IdempotencyTestCommitMode.BeforeCommit or
                        IdempotencyTestCommitMode.DuringCommit)
                    {
                        throw new IdempotencyTestException(
                            "Test hook: bağlantı commit sırasında koptu.");
                    }

                    await scope.CommitAsync(context.RequestAborted);
                    committed = true;

                    if (hook?.CommitMode == IdempotencyTestCommitMode.AfterCommit)
                    {
                        throw new IdempotencyTestException(
                            "Test hook: commit sonrası yanıt yazılmadan bağlantı koptu.");
                    }
                }
                catch (Exception exception)
                {
                    // Commit sonucu belirsiz: aynı anahtarla retry güvenlidir (§4.2.1).
                    logger.LogError(exception, "Idempotency commit sonucu belirsiz; key={Key}", key);
                    await IdempotencyProblems.InProgress(context).ExecuteAsync(context);
                    return;
                }
            }

            context.Response.ContentLength = responseBytes.Length;
            await originalBody.WriteAsync(responseBytes, context.RequestAborted);
        }
        catch (Exception)
        {
            context.Response.Body = originalBody;
            throw;
        }
        finally
        {
            scopeSetter.Set(null);
            idempotencyDb.Database.UseTransaction((DbTransaction?)null);
            foreach (var participant in enlistedParticipants)
            {
                try
                {
                    participant.Enlist(null);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Modül transaction bağlantısı temizlenemedi.");
                }
            }

            if (scope is not null)
            {
                try
                {
                    await scope.DisposeAsync();
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Idempotency transaction kapatılamadı; key={Key}", key);
                }
            }

            buffer?.Dispose();
            _activeKeys.TryRemove(activeKey, out _);
        }
    }

    /// <summary> Var kayıt için replay / key reuse / expired / in-progress kararı. Commit yapıldıysa true. </summary>
    private static async Task<bool> HandleExistingAsync(
        HttpContext context,
        IIdempotencyStore store,
        IWriteTransactionScope scope,
        IdempotencyKeyRecord existing,
        string scopeHash,
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (existing.Status == IdempotencyStatus.InProgress)
        {
            await IdempotencyProblems.InProgress(context).ExecuteAsync(context);
            return false;
        }

        if (existing.Status == IdempotencyStatus.Tombstone)
        {
            await IdempotencyProblems.Expired(context).ExecuteAsync(context);
            return false;
        }

        if (existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            //Tam gövde TTL'si doldu: gövdesiz tombstone + tek commit.
            await store.TombstoneAsync(scopeHash, key, cancellationToken);
            await scope.CommitAsync(cancellationToken);
            await IdempotencyProblems.Expired(context).ExecuteAsync(context);
            return true;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal) ||
            existing.ResponseBody is null || existing.ResponseStatusCode is null)
        {
            await IdempotencyProblems.KeyReused(context).ExecuteAsync(context);
            return false;
        }

        context.Response.StatusCode = existing.ResponseStatusCode.Value;
        context.Response.ContentType = existing.ResponseContentType;
        foreach (var (name, value) in ParseStoredHeaders(existing.ResponseHeaders))
        {
            context.Response.Headers[name] = value;
        }

        var bytes = Encoding.UTF8.GetBytes(existing.ResponseBody);
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, cancellationToken);
        return false;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseStoredHeaders(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                .Select(parts => new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()));

    private static bool IsInScope(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return InScopePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
