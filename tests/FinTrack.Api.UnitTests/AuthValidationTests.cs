using FinTrack.Api.Identity;
using Xunit;

namespace FinTrack.Api.UnitTests;

public sealed class AuthValidationTests
{
    [Fact]
    public void Gecerli_kayit_istegi_hatasiz()
    {
        var errors = AuthValidation.ValidateRegister(
            new RegisterRequest("user@example.com", "GucluParola1", "Europe/Istanbul"));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("duz-metin")]
    public void Gecersiz_eposta_hata_doner(string? email)
    {
        var errors = AuthValidation.ValidateRegister(
            new RegisterRequest(email, "GucluParola1", null));

        Assert.True(errors.ContainsKey("email"));
    }

    [Theory]
    [InlineData("Kisa1")]
    [InlineData("parolasiz")]
    [InlineData("12345678")]
    [InlineData("GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1GucluParola1")]
    public void Zayif_parola_hata_doner(string password)
    {
        var errors = AuthValidation.ValidateRegister(
            new RegisterRequest("user@example.com", password, null));

        Assert.True(errors.ContainsKey("password"));
    }

    [Theory]
    [InlineData("Europe/Istanbul")]
    [InlineData("America/New_York")]
    public void Gecerli_saat_dilimi_kabul_edilir(string timeZone)
    {
        var errors = AuthValidation.ValidateRegister(
            new RegisterRequest("user@example.com", "GucluParola1", timeZone));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("Europe/Istanbul/Extra")]
    public void Gecersiz_saat_dilimi_hata_doner(string timeZone)
    {
        var errors = AuthValidation.ValidateRegister(
            new RegisterRequest("user@example.com", "GucluParola1", timeZone));

        Assert.True(errors.ContainsKey("preferredTimeZone"));
    }

    [Fact]
    public void Gecerli_rowVersion_cozulur()
    {
        var value = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.True(AuthValidation.TryParseRowVersion(value, out var rowVersion));
        Assert.Equal(8, rowVersion.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gecersiz")]
    [InlineData("AQIDBAUGBwgJ")]
    public void Gecersiz_rowVersion_reddedilir(string? value)
    {
        // Son örnek 8 değil 9 bayttır; SQL Server rowversion her zaman 8 bayttır.
        Assert.False(AuthValidation.TryParseRowVersion(value, out _));
    }

    [Fact]
    public void Profil_guncelleme_zorunlu_alanlari_dogrular()
    {
        var errors = AuthValidation.ValidateUpdateProfile(new UpdateProfileRequest(null, null));

        Assert.True(errors.ContainsKey("preferredTimeZone"));
        Assert.True(errors.ContainsKey("rowVersion"));
    }

    [Fact]
    public void Login_bos_alanlari_dogrular()
    {
        var errors = AuthValidation.ValidateLogin(new LoginRequest("", null));

        Assert.True(errors.ContainsKey("email"));
        Assert.True(errors.ContainsKey("password"));
    }

    [Fact]
    public void Refresh_token_zorunludur()
    {
        var errors = AuthValidation.ValidateRefreshToken(" ");

        Assert.True(errors.ContainsKey("refreshToken"));
    }
}
