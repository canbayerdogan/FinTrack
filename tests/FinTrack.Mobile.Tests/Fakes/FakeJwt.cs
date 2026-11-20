using System.Text;
using System.Text.Json;

namespace FinTrack.Mobile.Tests.Fakes;

/// <summary> Testler için yalnızca <c>sub</c> claim'i taşıyan JWT benzeri access token. </summary>
public static class FakeJwt
{
    public static string WithSubject(string subject)
    {
        var header = Encode(new { alg = "none", typ = "JWT" });
        var payload = Encode(new { sub = subject });
        return $"{header}.{payload}.signature";
    }

    private static string Encode(object value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
