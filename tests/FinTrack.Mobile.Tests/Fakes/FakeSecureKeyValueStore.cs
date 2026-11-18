using FinTrack.Mobile.Core.Abstractions;

namespace FinTrack.Mobile.Tests.Fakes;

/// <summary> SecureStorage yerine deterministik, bellekteki anahtar deposu. </summary>
public sealed class FakeSecureKeyValueStore : ISecureKeyValueStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Values => _values;

    public int SetCount { get; private set; }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        SetCount++;
        return Task.CompletedTask;
    }

    public void Remove(string key) => _values.Remove(key);
}
