using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinTrack.SharedKernel;

namespace FinTrack.Api.Identity;

/// <summary>
/// Kullanıcı kimliği yalnızca doğrulanmış JWT claim'inden okunur.
/// İstemcinin gövdede/başlıkta gönderdiği kullanıcı bilgisine güvenilmez.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public bool IsAuthenticated => UserId is not null;

    public string PreferredTimeZone
    {
        get
        {
            var value = accessor.HttpContext?.User?.FindFirstValue("preferred_timezone");
            return string.IsNullOrWhiteSpace(value) ? ApplicationUser.DefaultTimeZone : value;
        }
    }

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("Kimliği doğrulanmış kullanıcı yok.");
}
