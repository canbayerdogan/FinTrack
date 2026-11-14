using System.Security.Cryptography;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace FinTrack.Api.Identity;

/// <summary>
/// Kimlik doğrulama uç noktaları. İş modülü değildir; host içinde yaşar.
/// </summary>
public static class AuthEndpoints
{
    public const string RoutePrefix = "/api/v1/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix).WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken")
            .AllowAnonymous();

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .AllowAnonymous();

        group.MapGet("/me", GetMeAsync)
            .WithName("GetProfile")
            .RequireAuthorization();

        group.MapPut("/me", UpdateMeAsync)
            .WithName("UpdateProfile")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        AuthTokenService tokenService,
        FinTrackIdentityDbContext db,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidation.ValidateRegister(request);
        if (errors.Count > 0)
        {
            return AuthProblems.Validation(http, errors);
        }

        var email = request.Email!.Trim();
        var timeZone = string.IsNullOrWhiteSpace(request.PreferredTimeZone)
            ? ApplicationUser.DefaultTimeZone
            : request.PreferredTimeZone.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return AuthProblems.EmailAlreadyRegistered(http);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            PreferredTimeZone = timeZone,
            CreatedAtUtc = clock.UtcNow,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password!);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AuthProblems.EmailAlreadyRegistered(http);
        }

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return AuthProblems.EmailAlreadyRegistered(http);
            }

            return AuthProblems.Validation(http, MapIdentityErrors(result));
        }

        var response = await tokenService.IssueAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"{RoutePrefix}/me", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        AuthTokenService tokenService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidation.ValidateLogin(request);
        if (errors.Count > 0)
        {
            return AuthProblems.Validation(http, errors);
        }

        var user = await userManager.FindByEmailAsync(request.Email!.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password!))
        {
            return AuthProblems.InvalidCredentials(http);
        }

        return Results.Ok(await tokenService.IssueAsync(user, cancellationToken));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        AuthTokenService tokenService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidation.ValidateRefreshToken(request.RefreshToken);
        if (errors.Count > 0)
        {
            return AuthProblems.Validation(http, errors);
        }

        var response = await tokenService.RotateAsync(request.RefreshToken!, cancellationToken);
        return response is null
            ? AuthProblems.InvalidRefreshToken(http)
            : Results.Ok(response);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        AuthTokenService tokenService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidation.ValidateRefreshToken(request.RefreshToken);
        if (errors.Count > 0)
        {
            return AuthProblems.Validation(http, errors);
        }

        await tokenService.RevokeAsync(request.RefreshToken!, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager,
        HttpContext http)
    {
        var user = await userManager.FindByIdAsync(currentUser.RequireUserId().ToString());
        return user is null ? AuthProblems.NotFound(http) : Results.Ok(ToProfile(user));
    }

    private static async Task<IResult> UpdateMeAsync(
        UpdateProfileRequest request,
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var errors = AuthValidation.ValidateUpdateProfile(request);
        if (errors.Count > 0)
        {
            return AuthProblems.Validation(http, errors);
        }

        var user = await userManager.FindByIdAsync(currentUser.RequireUserId().ToString());
        if (user is null)
        {
            return AuthProblems.NotFound(http);
        }

        AuthValidation.TryParseRowVersion(request.RowVersion, out var requestedRowVersion);
        if (user.RowVersion.Length != requestedRowVersion.Length ||
            !CryptographicOperations.FixedTimeEquals(user.RowVersion, requestedRowVersion))
        {
            return AuthProblems.ConcurrencyConflict(http);
        }

        user.PreferredTimeZone = request.PreferredTimeZone!.Trim();

        try
        {
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return AuthProblems.Validation(http, MapIdentityErrors(result));
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthProblems.ConcurrencyConflict(http);
        }

        return Results.Ok(ToProfile(user));
    }

    private static ProfileResponse ToProfile(ApplicationUser user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.PreferredTimeZone,
            Convert.ToBase64String(user.RowVersion));

    private static Dictionary<string, string[]> MapIdentityErrors(IdentityResult result)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in result.Errors)
        {
            var field = error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                        error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase)
                ? "email"
                : "password";

            errors[field] = errors.TryGetValue(field, out var existing)
                ? [.. existing, error.Description]
                : [error.Description];
        }

        return errors;
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
