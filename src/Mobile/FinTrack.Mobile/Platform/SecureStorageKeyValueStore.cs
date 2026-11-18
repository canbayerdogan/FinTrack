using FinTrack.Mobile.Core.Abstractions;
using Microsoft.Maui.Storage;

namespace FinTrack.Mobile.Platform;

/// <summary>
/// Yalnızca bekleyen işlem şifreleme anahtarları için MAUI SecureStorage
/// uyarlaması. Bekleyen işlem verisi SQLite'ta tutulur.
/// </summary>
public sealed class SecureStorageKeyValueStore : ISecureKeyValueStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        SecureStorage.GetAsync(key);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
        SecureStorage.SetAsync(key, value);
}
