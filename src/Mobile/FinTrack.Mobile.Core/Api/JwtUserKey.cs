using System.Text.Json;

namespace FinTrack.Mobile.Core.Api;

/// <summary>
/// Yerel pending-operation kullanıcı anahtarı, access token'daki JWT <c>sub</c>
/// claim'inden türetilir (docs/11 §7.5). Token hiçbir yere loglanmaz; geçersiz
/// veya süresi dolmuş bir token kullanıcı anahtarı üretmez.
/// </summary>
public static class JwtUserKey
{
    public static string? TryGetSubject(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var segments = accessToken.Split('.');
        if (segments.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = DecodeBase64Url(segments[1]);
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("sub", out var subject) &&
                subject.ValueKind == JsonValueKind.String)
            {
                var value = subject.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch (FormatException)
        {
            // Token base64url değil; oturum bağlamı yok sayılır.
        }
        catch (JsonException)
        {
            // Token payload'u JSON değil; oturum bağlamı yok sayılır.
        }

        return null;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padding = (4 - value.Length % 4) % 4;
        var base64 = value.Replace('-', '+').Replace('_', '/') + new string('=', padding);
        return Convert.FromBase64String(base64);
    }
}
