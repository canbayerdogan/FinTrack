using System.Security.Cryptography;

namespace FinTrack.SharedKernel;

/// <summary>
/// SQL Server <c>rowversion</c> değerinin API sözleşmesindeki base64 karşılığı.
/// Her zaman 8 bayttır; farklı uzunluk geçersiz sayılır.
/// </summary>
public static class RowVersionCodec
{
    public const int Length = 8;

    public static bool TryParse(string? value, out byte[] rowVersion)
    {
        rowVersion = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != Length)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }

    public static string ToBase64(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    /// <summary>Sabit zamanlı karşılaştırma; istemciden gelen sürümü doğrular.</summary>
    public static bool Matches(byte[] current, byte[] requested) =>
        current.Length == requested.Length &&
        CryptographicOperations.FixedTimeEquals(current, requested);
}
