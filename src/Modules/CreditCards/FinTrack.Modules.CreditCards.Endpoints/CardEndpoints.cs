using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.CreditCards.Endpoints;

internal static class CardEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("", async (
                string? status,
                IQueryHandler<ListCardsQuery, Result<IReadOnlyList<CardDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var parsedStatus = ParseStatus(status, errors);

                if (errors.Count > 0)
                {
                    return CreditCardsProblems.Validation(context, ToErrors(errors));
                }

                var result = await handler.HandleAsync(
                    new ListCardsQuery(new CardFilter(parsedStatus)),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("ListCards")
            .WithSummary("Kartları listeler; varsayılan yalnızca aktif kartlardır.");

        group.MapPost("", async (
                CreateCardRequest request,
                ICommandHandler<CreateCardCommand, Result<CardDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreateCardCommand(
                        request.BankName,
                        request.DisplayName,
                        request.LogoKey,
                        request.CreditLimit,
                        request.StatementDay,
                        request.PaymentDueDay),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"{CreditCardsModule.RoutePrefix}/{result.Value.Id}", result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("CreateCard")
            .WithSummary("Kart oluşturur; kart numarası/CVV kabul edilmez.");

        group.MapGet("/{id:guid}", async (
                Guid id,
                IQueryHandler<GetCardQuery, Result<CardDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetCardQuery(id), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("GetCard")
            .WithSummary("Tek kartı getirir.");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCardRequest request,
                ICommandHandler<UpdateCardCommand, Result<CardDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new UpdateCardCommand(
                        id,
                        request.BankName,
                        request.DisplayName,
                        request.LogoKey,
                        request.CreditLimit,
                        request.StatementDay,
                        request.PaymentDueDay,
                        request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("UpdateCard")
            .WithSummary("Kart tanımını günceller.");

        group.MapPost("/{id:guid}/archive", async (
                Guid id,
                ArchiveCardRequest request,
                ICommandHandler<ArchiveCardCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new ArchiveCardCommand(id, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("ArchiveCard")
            .WithSummary("Kartı arşivler; hard delete yoktur.");

        group.MapGet("/{id:guid}/balance", async (
                Guid id,
                string? asOf,
                IQueryHandler<GetCardBalanceQuery, Result<CardBalanceDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var parsedAsOf = ParseDate(asOf, "asOf", errors);

                if (errors.Count > 0)
                {
                    return CreditCardsProblems.Validation(context, ToErrors(errors));
                }

                var result = await handler.HandleAsync(
                    new GetCardBalanceQuery(id, parsedAsOf),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("GetCardBalance")
            .WithSummary("Kart borcunu kesitli hesaplar; asOf verilmezse kullanıcının bugünü kullanılır.");
    }

    private static DateOnly? ParseDate(
        string? value,
        string field,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParseDate(value, out var parsed))
        {
            AddError(errors, field, "Tarih yyyy-MM-dd biçiminde olmalıdır.");
        }

        return parsed;
    }

    private static CardStatus? ParseStatus(
        string? value,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParseCardStatus(value, out var parsed))
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
