using FinTrack.Api.Identity;
using FinTrack.SharedKernel;

namespace FinTrack.Api.Idempotency;

/// <summary> GET /api/v1/idempotency/{key} — salt okunur, kullanıcı izolasyonlu durum sorgusu (docs/11 §7.5). </summary>
public static class IdempotencyEndpoints
{
    public const string RoutePrefix = "/api/v1/idempotency";

    public static IEndpointRouteBuilder MapIdempotencyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix).RequireAuthorization();

        group.MapGet("/{key}", async (
            string key,
            HttpContext context,
            IIdempotencyStore store,
            ICurrentUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(key, out var parsed))
            {
                return IdempotencyProblems.Validation(
                    context, "idempotencyKey", "Anahtar geçerli bir UUID olmalıdır.");
            }

            Guid userId;
            try
            {
                userId = currentUser.RequireUserId();
            }
            catch (UnauthorizedAccessException)
            {
                return AuthProblems.Unauthorized(context);
            }

            var result = await store.GetStatusAsync(userId.ToString(), parsed.ToString("D"), cancellationToken);
            if (result is null)
            {
                return Results.NotFound();
            }

            return Results.Json(new
            {
                status = result.Status.ToString(),
                completedAtUtc = result.CompletedAtUtc,
                hasBody = result.HasBody && result.Status == IdempotencyStatus.Completed &&
                          result.ExpiresAtUtc > DateTime.UtcNow,
            });
        });

        return endpoints;
    }
}
