using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.CreditCards.Endpoints;

internal static class StatementEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/statements", async (
                Guid id,
                string? status,
                bool? includeCancelled,
                int? page,
                int? pageSize,
                IQueryHandler<ListStatementsQuery, Result<PagedResult<StatementDto>>> handler,
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

                var filter = new StatementFilter(
                    parsedStatus,
                    includeCancelled ?? false,
                    resolvedPage,
                    resolvedPageSize);

                var result = await handler.HandleAsync(
                    new ListStatementsQuery(id, filter),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("ListStatements")
            .WithSummary("Kartın ekstrelerini listeler; varsayılan yalnızca aktif ekstrelerdir.");

        group.MapGet("/{id:guid}/statements/{statementId:guid}", async (
                Guid id,
                Guid statementId,
                IQueryHandler<GetStatementQuery, Result<StatementDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new GetStatementQuery(id, statementId),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("GetStatement")
            .WithSummary("Tek ekstreyi türetilen alanlarıyla getirir.");

        group.MapPost("/{id:guid}/statements", async (
                Guid id,
                CreateStatementRequest request,
                ICommandHandler<CreateStatementCommand, Result<StatementDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreateStatementCommand(
                        id,
                        request.PeriodStart,
                        request.PeriodEnd,
                        request.DueDate,
                        request.StatementAmount,
                        request.Currency),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created(
                        $"{CreditCardsModule.RoutePrefix}/{id}/statements/{result.Value.Id}",
                        result.Value)
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("CreateStatement")
            .WithSummary("Ekstre oluşturur; türetilen alanlar istemciden kabul edilmez.");

        group.MapPost("/{id:guid}/statements/{statementId:guid}/cancel", async (
                Guid id,
                Guid statementId,
                CancelRequest request,
                ICommandHandler<CancelStatementCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelStatementCommand(id, statementId, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : CreditCardsProblems.FromError(context, result.Error);
            })
            .WithName("CancelStatement")
            .WithSummary("Ekstreyi iptal eder; hard delete yoktur, dönem serbest kalır.");
    }

    private static StatementStatus? ParseStatus(
        string? value,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParseStatementStatus(value, out var parsed))
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
