using System.Security.Cryptography;
using System.Text;

namespace FinTrack.Mobile.Core.Abstractions;

/// <summary>
/// Yalnızca küçük gizli değerler (bekleyen işlem şifreleme anahtarları) için
/// platform güvenli depolama sözleşmesi. Veri satırları burada saklanmaz.
/// </summary>
public interface ISecureKeyValueStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Şifreleme anahtarlarının kullanıcı bazlı adları. Kullanıcı kimliği açıkça
/// depolanmaz; anahtar adı SHA-256 ile türetilir.
/// </summary>
public static class PendingOperationKeyNames
{
    private const string Prefix = "fintrack.pending.encryption-key.";

    public static string ForUser(string userIdKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userIdKey));
        return Prefix + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
