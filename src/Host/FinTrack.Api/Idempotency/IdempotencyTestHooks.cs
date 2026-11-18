using System.Collections.Concurrent;

namespace FinTrack.Api.Idempotency;

/// <summary>
/// Testler için commit sınırı denetimi. Production yolunda kayıt yoksa davranış
/// değişmez; yalnızca entegrasyon testleri anahtar bazlı hook kaydeder.
/// </summary>
internal interface IIdempotencyTestHooks
{
    IdempotencyTestHook? Get(string key);

    void Set(string key, IdempotencyTestHook hook);

    void Clear(string key);
}

internal sealed class IdempotencyTestHook
{
    /// <summary>Commit hemen öncesinde bekletmek için kullanılır.</summary>
    public Func<Task>? BeforeCommitAsync { get; init; }

    /// <summary>Commit sonucunu belirsiz hale getiren hata noktası.</summary>
    public IdempotencyTestCommitMode CommitMode { get; init; }

    /// <summary>INSERT sonucunu zorla (çoklu instance lock senaryoları).</summary>
    public IdempotencyInsertOutcome? ForcedInsertOutcome { get; init; }

    /// <summary>2xx yanıtını test amacıyla büyütür (S20).</summary>
    public string? ResponseBodyOverride { get; init; }
}

internal enum IdempotencyTestCommitMode
{
    None,
    BeforeCommit,
    DuringCommit,
    AfterCommit,
}

internal sealed class IdempotencyTestException(string message) : Exception(message);

internal sealed class IdempotencyTestHooks : IIdempotencyTestHooks
{
    private readonly ConcurrentDictionary<string, IdempotencyTestHook> _hooks = new(StringComparer.Ordinal);

    public IdempotencyTestHook? Get(string key) => _hooks.TryGetValue(key, out var hook) ? hook : null;

    public void Set(string key, IdempotencyTestHook hook) => _hooks[key] = hook;

    public void Clear(string key) => _hooks.TryRemove(key, out _);
}
