namespace FinTrack.Api.Identity;

public sealed record RegisterRequest(string? Email, string? Password, string? PreferredTimeZone);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record UpdateProfileRequest(string? PreferredTimeZone, string? RowVersion);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string PreferredTimeZone,
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType);

public sealed record ProfileResponse(
    Guid UserId,
    string Email,
    string PreferredTimeZone,
    string RowVersion);
