using System.IdentityModel.Tokens.Jwt;
using FinTrack.Api.Identity;
using FinTrack.SharedKernel;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FinTrack.Api.UnitTests;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "FinTrack.Tests",
        Audience = "FinTrack.Tests",
        SigningKey = "test-signing-key-that-is-long-enough-1234567890",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    };

    [Fact]
    public void Access_token_claimleri_ve_suresi_dogru()
    {
        var now = new DateTime(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PreferredTimeZone = "Europe/Istanbul",
        };

        var service = new JwtTokenService(Options, new FixedClock(now));

        var result = service.CreateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(900, result.ExpiresInSeconds);
        Assert.Equal("FinTrack.Tests", token.Issuer);
        Assert.Contains("FinTrack.Tests", token.Audiences);
        Assert.Equal(user.Id.ToString(), ClaimValue(token, JwtRegisteredClaimNames.Sub));
        Assert.Equal("user@example.com", ClaimValue(token, JwtRegisteredClaimNames.Email));
        Assert.Equal("Europe/Istanbul", ClaimValue(token, "preferred_timezone"));
        Assert.False(string.IsNullOrWhiteSpace(ClaimValue(token, JwtRegisteredClaimNames.Jti)));
        Assert.Equal(EpochTime.GetIntDate(now), long.Parse(ClaimValue(token, JwtRegisteredClaimNames.Iat)));
        Assert.Equal(EpochTime.GetIntDate(now.AddMinutes(15)), token.Payload.Expiration!.Value);
    }

    [Fact]
    public void Refresh_token_rastgele_ve_hash_tekil()
    {
        var first = JwtTokenService.CreateRefreshToken();
        var second = JwtTokenService.CreateRefreshToken();

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
        Assert.All(first, character => Assert.True(Uri.IsHexDigit(character)));

        var hash = JwtTokenService.HashRefreshToken(first);
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(first, hash);
        Assert.Equal(hash, JwtTokenService.HashRefreshToken(first));
    }

    private static string ClaimValue(JwtSecurityToken token, string claimType) =>
        token.Claims.Single(claim => claim.Type == claimType).Value;

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
