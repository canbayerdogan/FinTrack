using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Application.Payments;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.CreditCards.Endpoints;

internal static class CardPaymentEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/payments", async (
                Guid id,
                string? status,
                bool? includeCancelled,
                int? page,
                int? pageSize,
                IQueryHandler<ListCardPaymentsQuery, Result<PagedResult<CardPaymentDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var parsedStatus = ParseStatus(status, errors);
                var resolvedPage = page ?? 1;
                var resolvedPageSize = pageSize ?? 50;

                if (resolvedPage < 1)
                {
                    AddError(errors, "page", "page 1 veya daha büyük olmalıdır.");
                }

                if (resolvedPageSize is < 1 or > 200)
                {
                    AddError(errors, "pageSize", "pageSize 1 ile 200 arasında olmalıdır.");
                }

                if (errors.Count > 0)
                {
                    return CreditCardsProblems.Validation(context, ToErrors(errors));
                }

                var filter = new CardPaymentFilter(
                    parsedStatus,
                    includeCancelled ?? false,
                    resolvedPage,
                    resolvedPageSize);

                var result = await handler.HandleAsync(
                    new ListCardPaymentsQuery(id, filter),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("ListCardPayments")
            .WithSummary("Kartın ödemelerini listeler; varsayılan yalnızca aktif ödemelerdir.");

        group.MapPost("/{id:guid}/payments", async (
                Guid id,
                CreateCardPaymentRequest request,
                ICommandHandler<CreateCardPaymentCommand, Result<CardPaymentDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreateCardPaymentCommand(
                        id,
                        request.Amount,
                        request.Currency,
                        request.PaidOn,
                        request.Note),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created(
                        $"{CreditCardsModule.RoutePrefix}/{id}/payments/{result.Value.Id}",
                        result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("CreateCardPayment")
            .WithSummary("Karta ödeme ekler; ödeme gider değildir.");

        group.MapPost("/{id:guid}/payments/{paymentId:guid}/cancel", async (
                Guid id,
                Guid paymentId,
                CancelRequest request,
                ICommandHandler<CancelCardPaymentCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelCardPaymentCommand(id, paymentId, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("CancelCardPayment")
            .WithSummary("Kart ödemesini iptal eder; gerekçe zorunludur.");
    }

    private static PaymentStatus? ParseStatus(
        string? value,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParsePaymentStatus(value, out var parsed))
        {
            AddError(errors, "status", "Geçerli bir durum seçilmelidir.");
        }

        return parsed;
    }

    private static void AddError(
        IDictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }

    private static Dictionary<string, string[]> ToErrors(IDictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
}
