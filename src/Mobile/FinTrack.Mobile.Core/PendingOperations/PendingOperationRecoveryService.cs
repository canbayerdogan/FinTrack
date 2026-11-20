using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Mobile.Core.Api;

namespace FinTrack.Mobile.Core.PendingOperations;

/// <summary>
/// Bekleyen işlemlerin kurtarma/zaman aşımı durum makinesi (docs/11 §7.4–7.5).
/// Created asla otomatik gönderilmez; yalnızca SentUnconfirmed kurtarılır.
/// </summary>
public sealed class PendingOperationRecoveryService
{
    public const int MaxAttempts = 3;
    public const string CompletedMessage = "İşlem tamamlandı; kayıtlarını kontrol edebilirsin.";
    public const string UnknownResultMessage =
        "İşlemin kesin sonucu bilinmiyor; lütfen kayıtlarını kontrol et.";
    public const string PendingMessage =
        "İşlemin sonucu henüz doğrulanmadı; aynı anahtarla tekrar denenecek.";

    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FullBodyTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan TombstoneTtl = TimeSpan.FromDays(30);

    private readonly HttpClient _http;
    private readonly IPendingOperationStore _store;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<CancellationToken, Task<string?>>? _currentUserKey;

    public PendingOperationRecoveryService(
        HttpClient http,
        IPendingOperationStore store,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<CancellationToken, Task<string?>>? currentUserKeyProvider = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? TimeProvider.System;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        _currentUserKey = currentUserKeyProvider;
    }

    /// <summary> Son kullanıcıya gösterilmesi beklenen mesaj. </summary>
    public string? LastMessage { get; private set; }

    /// <summary> Kullanıcı hareketiyle yeni kayıt oluşturur (Created, ağa çıkmaz). </summary>
    public Task<PendingOperation> CreateAsync(
        string userIdKey,
        string method,
        string route,
        byte[] body,
        CancellationToken cancellationToken = default) =>
        _store.AddAsync(userIdKey, method, route, body, cancellationToken);

    /// <summary> Kullanıcının açık "Tekrar dene" hareketiyle aynı kaydı gönderir. </summary>
    public async Task<RecoveryResult> SendAsync(string key, CancellationToken cancellationToken = default)
    {
        var operation = await _store.GetAsync(key, cancellationToken);
        if (operation is null)
        {
            return Result(RecoveryStatus.Skipped, key, "Bekleyen işlem kaydı bulunamadı.");
        }

        if (operation.State == PendingOperationState.Uncertain)
        {
            return Result(RecoveryStatus.Uncertain, key, UnknownResultMessage);
        }

        if (operation.State == PendingOperationState.Created)
        {
            // Ağa çıkmadan önce kalıcı hale getir; restart'ta aynı key/body replay edilir.
            await _store.SetStateAsync(key, PendingOperationState.SentUnconfirmed, null, cancellationToken);
        }

        return await ExecuteAsync(operation with { State = PendingOperationState.SentUnconfirmed }, cancellationToken);
    }

    /// <summary> Uygulama açılışında yalnızca SentUnconfirmed kayıtları kurtarır. </summary>
    public async Task<RecoveryReport> RecoverAsync(
        string userIdKey,
        CancellationToken cancellationToken = default)
    {
        var operations = await _store.ListForUserAsync(
            userIdKey,
            PendingOperationState.SentUnconfirmed,
            cancellationToken);
        var results = new List<RecoveryResult>(operations.Count);
        foreach (var operation in operations)
        {
            results.Add(await RecoverOneAsync(operation.Key, cancellationToken));
        }

        return new RecoveryReport(results);
    }

    public async Task<RecoveryResult> RecoverOneAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var operation = await _store.GetAsync(key, cancellationToken);
        if (operation is null || operation.State != PendingOperationState.SentUnconfirmed)
        {
            return Result(RecoveryStatus.Skipped, key, null);
        }

        return await ExecuteAsync(operation, cancellationToken);
    }

    /// <summary>
    /// Aynı kullanıcı/method/route ve aynı gövde baytlarıyla temsil edilen mevcut
    /// işlemi bulur. Şifre çözülemeyen kayıt, gövde karşılaştırması yapılamadığı
    /// için "yeni anahtar üretme" tarafında döndürülür; kurtarma onu Uncertain
    /// işaretler (docs/11 §7.2/§7.5). Böylece aynı kullanıcı hareketi için ikinci
    /// UUID ve ikinci finansal POST oluşmaz.
    /// </summary>
    public async Task<PendingOperation?> FindMatchingAsync(
        string userIdKey,
        string method,
        string route,
        byte[] body,
        CancellationToken cancellationToken = default)
    {
        var operations = await _store.ListForUserAsync(userIdKey, cancellationToken: cancellationToken);
        foreach (var operation in operations)
        {
            if (!string.Equals(operation.Method, method, StringComparison.OrdinalIgnoreCase) ||
                !SameRoute(operation.Route, route))
            {
                continue;
            }

            var storedBody = await _store.TryReadBodyAsync(operation, cancellationToken);
            if (storedBody is null || storedBody.AsSpan().SequenceEqual(body))
            {
                return operation;
            }
        }

        return null;
    }

    private static bool SameRoute(string left, string right) =>
        string.Equals(left.TrimStart('/'), right.TrimStart('/'), StringComparison.OrdinalIgnoreCase);

    private async Task<RecoveryResult> ExecuteAsync(
        PendingOperation operation,
        CancellationToken cancellationToken)
    {
        // Oturum kullanıcı anahtarı, kaydın sahibiyle aynı olmalıdır. Oturum
        // değişirse kayıt korunur; başka kullanıcının token'ıyla gönderilmez.
        if (_currentUserKey is not null)
        {
            var currentUserKey = await _currentUserKey(cancellationToken);
            if (!string.Equals(currentUserKey, operation.UserIdKey, StringComparison.Ordinal))
            {
                return Result(
                    RecoveryStatus.Pending,
                    operation.Key,
                    "Oturum değişti; bekleyen işlem korundu.");
            }
        }

        var body = await _store.TryReadBodyAsync(operation, cancellationToken);
        if (body is null)
        {
            // Anahtar kaybı / AAD bozulması: silme yok, yeni UUID yok.
            return await MarkUncertainAsync(operation, cancellationToken);
        }

        var now = _clock.GetUtcNow();
        var age = now - operation.CreatedAtUtc;
        if (age >= TombstoneTtl)
        {
            return await MarkUncertainAsync(operation, cancellationToken);
        }

        if (age >= FullBodyTtl)
        {
            return await CheckStatusAsync(operation, now, cancellationToken);
        }

        // Sınır her kurtarma/deneme akışı için geçerlidir; kullanıcının açık
        // "Tekrar dene" hareketi aynı anahtarla yeni bir akış başlatabilir.
        string? lastError = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await _store.IncrementAttemptAsync(operation.Key, null, cancellationToken);
            try
            {
                using var response = await SendPostAsync(operation, body, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await _store.DeleteAsync(operation.Key, cancellationToken);
                    return Result(RecoveryStatus.Completed, operation.Key, CompletedMessage);
                }

                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                {
                    var problem = await ApiException.FromResponseAsync(response, cancellationToken);
                    if (IsDefinitiveFailure(problem))
                    {
                        await _store.DeleteAsync(operation.Key, cancellationToken);
                        return Result(RecoveryStatus.DefiniteFailure, operation.Key, problem.UserMessage);
                    }

                    await _store.SetStateAsync(
                        operation.Key,
                        PendingOperationState.SentUnconfirmed,
                        problem.Code,
                        cancellationToken);
                    lastError = problem.Code;
                    if (string.Equals(problem.Code, "IdempotencyExpired", StringComparison.Ordinal))
                    {
                        return await CheckStatusAsync(operation, now, cancellationToken);
                    }

                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return Result(
                        RecoveryStatus.Pending,
                        operation.Key,
                        "Oturum süresi doldu; giriş yapıldıktan sonra aynı anahtarla devam edilecek.");
                }

                lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException)
            {
                lastError = "Ağ hatası.";
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = "İstek zaman aşımına uğradı.";
            }

            if (attempt < MaxAttempts - 1)
            {
                await _delay(Backoff(attempt), cancellationToken);
            }
        }

        await _store.SetStateAsync(
            operation.Key,
            PendingOperationState.SentUnconfirmed,
            lastError,
            cancellationToken);
        return Result(RecoveryStatus.Pending, operation.Key, PendingMessage);
    }

    private async Task<HttpResponseMessage> SendPostAsync(
        PendingOperation operation,
        byte[] body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, operation.Route)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operation.Key);
        return await _http.SendAsync(request, cancellationToken);
    }

    private async Task<RecoveryResult> CheckStatusAsync(
        PendingOperation operation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await _store.MarkTombstoneCheckedAsync(operation.Key, now, cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"idempotency/{Uri.EscapeDataString(operation.Key)}");
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<StatusPayload>(
                    ApiJson.Options,
                    cancellationToken);
                if (string.Equals(payload?.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    await _store.DeleteAsync(operation.Key, cancellationToken);
                    return Result(RecoveryStatus.Completed, operation.Key, CompletedMessage);
                }

                if (string.Equals(payload?.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
                {
                    return Result(RecoveryStatus.Pending, operation.Key, PendingMessage);
                }

                return await MarkUncertainAsync(operation, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return await MarkUncertainAsync(operation, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Result(
                    RecoveryStatus.Pending,
                    operation.Key,
                    "Oturum süresi doldu; giriş yapıldıktan sonra durum sorgulanacak.");
            }

            return Result(RecoveryStatus.Pending, operation.Key, PendingMessage);
        }
        catch (HttpRequestException)
        {
            return Result(RecoveryStatus.Pending, operation.Key, PendingMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result(RecoveryStatus.Pending, operation.Key, PendingMessage);
        }
        catch (JsonException)
        {
            return await MarkUncertainAsync(operation, cancellationToken);
        }
    }

    private async Task<RecoveryResult> MarkUncertainAsync(
        PendingOperation operation,
        CancellationToken cancellationToken)
    {
        await _store.SetStateAsync(
            operation.Key,
            PendingOperationState.Uncertain,
            UnknownResultMessage,
            cancellationToken);
        return Result(RecoveryStatus.Uncertain, operation.Key, UnknownResultMessage);
    }

    private static bool IsDefinitiveFailure(ApiException problem)
    {
        if (problem.StatusCode == (int)HttpStatusCode.BadRequest)
        {
            return true;
        }

        return problem.StatusCode == (int)HttpStatusCode.Conflict &&
               string.Equals(problem.Code, "IdempotencyKeyReused", StringComparison.Ordinal);
    }

    private static TimeSpan Backoff(int attempt)
    {
        var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (1L << attempt));
        return delay > MaxDelay ? MaxDelay : delay;
    }

    private RecoveryResult Result(RecoveryStatus status, string key, string? message)
    {
        LastMessage = message;
        return new RecoveryResult(status, key, message);
    }

    private sealed record StatusPayload(string? Status);
}
