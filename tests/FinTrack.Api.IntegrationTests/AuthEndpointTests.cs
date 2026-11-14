using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinTrack.Api.IntegrationTests;

/// <summary>
/// Kimlik doğrulama sözleşmesi (docs/06 §2) ve docs/07 slice 1 kabul testleri.
/// Her test kendi benzersiz e-postasını üretir; testler sıraya bağımlı değildir.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthEndpointTests(ApiFixture fixture)
{
    private const string AuthBase = "/api/v1/auth";
    private const string Password = "GucluParola1";

    [Fact]
    public async Task OpenApi_belgesi_auth_uc_noktalarini_icerir()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/register", document);
        Assert.Contains("/api/v1/auth/refresh", document);
        Assert.Contains("/api/v1/auth/me", document);
    }

    [Fact]
    public async Task Register_basarili_ve_access_token_claimleri_dogru()
    {
        using var client = CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync($"{AuthBase}/register", new
        {
            email,
            password = Password,
            preferredTimeZone = "Europe/Istanbul",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(email, auth.Email);
        Assert.Equal("Europe/Istanbul", auth.PreferredTimeZone);
        Assert.Equal(900, auth.ExpiresIn);
        Assert.Equal("Bearer", auth.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        var payload = DecodePayload(auth.AccessToken);
        Assert.Equal(auth.UserId.ToString(), payload.GetProperty("sub").GetString());
        Assert.Equal(email, payload.GetProperty("email").GetString());
        Assert.Equal(900, payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64());
    }

    [Fact]
    public async Task Register_saat_dilimi_verilmezse_varsayilan_istanbul()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync($"{AuthBase}/register", new
        {
            email = NewEmail(),
            password = Password,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal("Europe/Istanbul", auth.PreferredTimeZone);
    }

    [Fact]
    public async Task Register_ayni_eposta_409_doner()
    {
        using var client = CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync($"{AuthBase}/register", new
        {
            email,
            password = "BaskaParola1",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EmailAlreadyRegistered", await ReadCodeAsync(response));
    }

    [Theory]
    [InlineData("Kisa1")]
    [InlineData("parolasiz")]
    [InlineData("12345678")]
    public async Task Register_zayif_parola_400_doner(string password)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync($"{AuthBase}/register", new
        {
            email = NewEmail(),
            password,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, "password"));
    }

    [Fact]
    public async Task Register_gecersiz_saat_dilimi_400_doner()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync($"{AuthBase}/register", new
        {
            email = NewEmail(),
            password = Password,
            preferredTimeZone = "Mars/Olympus",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, "preferredTimeZone"));
    }

    [Fact]
    public async Task Login_dogru_parola_basarili_yanlis_parola_401()
    {
        using var client = CreateClient();
        var auth = await RegisterAsync(client, NewEmail());

        var success = await client.PostAsJsonAsync($"{AuthBase}/login", new
        {
            email = auth.Email,
            password = Password,
        });
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        var wrongPassword = await client.PostAsJsonAsync($"{AuthBase}/login", new
        {
            email = auth.Email,
            password = "YanlisParola1",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("InvalidCredentials", await ReadCodeAsync(wrongPassword));

        var unknownEmail = await client.PostAsJsonAsync($"{AuthBase}/login", new
        {
            email = NewEmail(),
            password = Password,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("InvalidCredentials", await ReadCodeAsync(unknownEmail));
    }

    [Fact]
    public async Task Refresh_rotasyonu_eski_tokeni_gecersiz_kilar()
    {
        using var client = CreateClient();
        var auth = await RegisterAsync(client, NewEmail());

        var refreshResponse = await client.PostAsJsonAsync($"{AuthBase}/refresh", new
        {
            refreshToken = auth.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.Equal(auth.UserId, refreshed.UserId);
        Assert.NotEqual(auth.RefreshToken, refreshed.RefreshToken);
        Assert.NotEqual(auth.AccessToken, refreshed.AccessToken);

        var reuse = await client.PostAsJsonAsync($"{AuthBase}/refresh", new
        {
            refreshToken = auth.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("InvalidRefreshToken", await ReadCodeAsync(reuse));

        var secondRefresh = await client.PostAsJsonAsync($"{AuthBase}/refresh", new
        {
            refreshToken = refreshed.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Logout_sonrasi_refresh_reddedilir_ve_logout_idempotenttir()
    {
        using var client = CreateClient();
        var auth = await RegisterAsync(client, NewEmail());

        var logout = await client.PostAsJsonAsync($"{AuthBase}/logout", new
        {
            refreshToken = auth.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync($"{AuthBase}/refresh", new
        {
            refreshToken = auth.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal("InvalidRefreshToken", await ReadCodeAsync(refresh));

        var logoutAgain = await client.PostAsJsonAsync($"{AuthBase}/logout", new
        {
            refreshToken = auth.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, logoutAgain.StatusCode);
    }

    [Fact]
    public async Task Gecersiz_refresh_token_401_eksik_alan_400()
    {
        using var client = CreateClient();

        var invalid = await client.PostAsJsonAsync($"{AuthBase}/refresh", new
        {
            refreshToken = "gecersiz-token",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal("InvalidRefreshToken", await ReadCodeAsync(invalid));

        var missing = await client.PostAsJsonAsync($"{AuthBase}/refresh", new
        {
            refreshToken = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(missing));
    }

    [Fact]
    public async Task Tokensiz_ve_bozuk_tokenli_korumali_uc_nokta_401()
    {
        using var client = CreateClient();

        var anonymous = await client.GetAsync($"{AuthBase}/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("Unauthorized", await ReadCodeAsync(anonymous));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "bozuk.token.degeri");
        var broken = await client.GetAsync($"{AuthBase}/me");
        Assert.Equal(HttpStatusCode.Unauthorized, broken.StatusCode);
        Assert.Equal("Unauthorized", await ReadCodeAsync(broken));
    }

    [Fact]
    public async Task Profil_guncelleme_rowversion_ile_calisir_eski_surum_409()
    {
        using var client = CreateClient();
        var auth = await RegisterAsync(client, NewEmail());
        Authorize(client, auth.AccessToken);

        var meResponse = await client.GetAsync($"{AuthBase}/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var profile = await meResponse.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(auth.UserId, profile.UserId);
        Assert.Equal(auth.Email, profile.Email);
        Assert.Equal("Europe/Istanbul", profile.PreferredTimeZone);
        Assert.Equal(8, Convert.FromBase64String(profile.RowVersion).Length);

        var update = await client.PutAsJsonAsync($"{AuthBase}/me", new
        {
            preferredTimeZone = "Europe/London",
            rowVersion = profile.RowVersion,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Europe/London", updated.PreferredTimeZone);
        Assert.NotEqual(profile.RowVersion, updated.RowVersion);

        var stale = await client.PutAsJsonAsync($"{AuthBase}/me", new
        {
            preferredTimeZone = "Europe/Berlin",
            rowVersion = profile.RowVersion,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConcurrencyConflict", await ReadCodeAsync(stale));

        var current = await client.GetFromJsonAsync<ProfileResponse>($"{AuthBase}/me");
        Assert.NotNull(current);
        Assert.Equal("Europe/London", current.PreferredTimeZone);
        Assert.Equal(updated.RowVersion, current.RowVersion);
    }

    [Fact]
    public async Task Profil_guncelleme_gecersiz_saat_dilimi_400()
    {
        using var client = CreateClient();
        var auth = await RegisterAsync(client, NewEmail());
        Authorize(client, auth.AccessToken);

        var profile = await client.GetFromJsonAsync<ProfileResponse>($"{AuthBase}/me");
        Assert.NotNull(profile);

        var response = await client.PutAsJsonAsync($"{AuthBase}/me", new
        {
            preferredTimeZone = "Mars/Olympus",
            rowVersion = profile.RowVersion,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", await ReadCodeAsync(response));
        Assert.True(await HasFieldErrorAsync(response, "preferredTimeZone"));
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_profilini_goremez()
    {
        using var clientA = CreateClient();
        using var clientB = CreateClient();

        var userA = await RegisterAsync(clientA, NewEmail());
        var userB = await RegisterAsync(clientB, NewEmail());

        Authorize(clientA, userA.AccessToken);
        // İstemci başka kullanıcı kimliği dayatmaya çalışsa da sunucu token'daki kullanıcıyı döner.
        var profileA = await clientA.GetFromJsonAsync<ProfileResponse>(
            $"{AuthBase}/me?userId={userB.UserId}");
        Assert.NotNull(profileA);
        Assert.Equal(userA.UserId, profileA.UserId);
        Assert.Equal(userA.Email, profileA.Email);

        Authorize(clientB, userB.AccessToken);
        var profileB = await clientB.GetFromJsonAsync<ProfileResponse>(
            $"{AuthBase}/me?userId={userA.UserId}");
        Assert.NotNull(profileB);
        Assert.Equal(userB.UserId, profileB.UserId);
        Assert.Equal(userB.Email, profileB.Email);
    }

    [Fact]
    public async Task Parola_ve_refresh_token_duz_metin_saklanmaz()
    {
        using var client = CreateClient();
        var auth = await RegisterAsync(client, NewEmail());

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinTrackIdentityDbContext>();

        var storedUser = await db.Users.SingleAsync(user => user.Id == auth.UserId);
        Assert.NotEqual(Password, storedUser.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(storedUser.PasswordHash));

        var storedToken = await db.RefreshTokens.SingleAsync(token => token.UserId == auth.UserId);
        Assert.NotEqual(auth.RefreshToken, storedToken.TokenHash);
        Assert.Equal(64, storedToken.TokenHash.Length);
    }

    private HttpClient CreateClient() => fixture.Factory.CreateClient();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string email,
        string? preferredTimeZone = "Europe/Istanbul")
    {
        var response = await client.PostAsJsonAsync($"{AuthBase}/register", new
        {
            email,
            password = Password,
            preferredTimeZone,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth;
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<bool> HasFieldErrorAsync(HttpResponseMessage response, string field)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("errors", out var errors) &&
               errors.ValueKind == JsonValueKind.Object &&
               errors.TryGetProperty(field, out _);
    }

    private static JsonElement DecodePayload(string token)
    {
        var segment = token.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');

        segment = segment.PadRight(segment.Length + ((4 - segment.Length % 4) % 4), '=');

        using var document = JsonDocument.Parse(Convert.FromBase64String(segment));
        return document.RootElement.Clone();
    }

    private sealed record AuthResponse(
        Guid UserId,
        string Email,
        string PreferredTimeZone,
        string AccessToken,
        string RefreshToken,
        int ExpiresIn,
        string TokenType);

    private sealed record ProfileResponse(
        Guid UserId,
        string Email,
        string PreferredTimeZone,
        string RowVersion);
}
