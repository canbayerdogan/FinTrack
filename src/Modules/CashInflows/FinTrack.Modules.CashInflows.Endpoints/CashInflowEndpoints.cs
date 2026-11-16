using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.CashInflows.Endpoints;

internal static class CashInflowEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("", async (
                string? from,
                string? to,
                string? source,
                string? status,
                bool? includeCancelled,
                int? page,
                int? pageSize,
                IQueryHandler<ListCashInflowsQuery, Result<PagedResult<CashInflowDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                var parsedFrom = ParseDate(from, "from", errors);
                var parsedTo = ParseDate(to, "to", errors);
                var parsedSource = ParseSource(source, errors);
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

                if (parsedFrom is not null && parsedTo is not null && parsedFrom > parsedTo)
                {
                    AddError(errors, "from", "from, to değerinden sonra olamaz.");
                }

                if (errors.Count > 0)
                {
                    return CashInflowsProblems.Validation(context, ToErrors(errors));
                }

                var filter = new CashInflowFilter(
                    parsedFrom,
                    parsedTo,
                    parsedSource,
                    parsedStatus,
                    includeCancelled ?? false,
                    resolvedPage,
                    resolvedPageSize);

                var result = await handler.HandleAsync(new ListCashInflowsQuery(filter), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CashInflowsProblems.FromError(context, result.Error);
            })
            .WithName("ListCashInflows")
            .WithSummary("Para girişlerini filtreli ve sayfalı listeler.");

        group.MapGet("/{id:guid}", async (
                Guid id,
                IQueryHandler<GetCashInflowQuery, Result<CashInflowDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetCashInflowQuery(id), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CashInflowsProblems.FromError(context, result.Error);
            })
            .WithName("GetCashInflow")
            .WithSummary("Tek para girişini getirir.");

        group.MapPost("", async (
                CreateCashInflowRequest request,
                ICommandHandler<CreateCashInflowCommand, Result<CashInflowDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var sourceValue = ParseSource(request.Source, errors);
                ValidateDerivedField(request.IsEarnedIncome, errors);

                if (errors.Count > 0)
                {
                    return CashInflowsProblems.Validation(context, ToErrors(errors));
                }

                var result = await handler.HandleAsync(
                    new CreateCashInflowCommand(
                        request.Amount,
                        request.Currency,
                        request.OccurredOn,
                        sourceValue,
                        request.Description),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"{CashInflowsModule.RoutePrefix}/{result.Value.Id}", result.Value)
                    : CashInflowsProblems.FromError(context, result.Error);
            })
            .WithName("CreateCashInflow")
            .WithSummary("Gelir veya diğer para girişi oluşturur; borç doğuran kaynaklar reddedilir.");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCashInflowRequest request,
                ICommandHandler<UpdateCashInflowCommand, Result<CashInflowDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var sourceValue = ParseSource(request.Source, errors);
                ValidateDerivedField(request.IsEarnedIncome, errors);

                if (request.RecordedAtUtc is not null)
                {
                    AddError(errors, "recordedAtUtc", "Oluşturulma zamanı değiştirilemez.");
                }

                if (errors.Count > 0)
                {
                    return CashInflowsProblems.Validation(context, ToErrors(errors));
                }

                var result = await handler.HandleAsync(
                    new UpdateCashInflowCommand(
                        id,
                        request.Amount,
                        request.Currency,
                        request.OccurredOn,
                        sourceValue,
                        request.Description,
                        request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : CashInflowsProblems.FromError(context, result.Error);
            })
            .WithName("UpdateCashInflow")
            .WithSummary("Borç bağlantısı olmayan para girişini günceller.");

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelRequest request,
                ICommandHandler<CancelCashInflowCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelCashInflowCommand(id, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : CashInflowsProblems.FromError(context, result.Error);
            })
            .WithName("CancelCashInflow")
            .WithSummary("Para girişini iptal eder; bağlantılı kayıtlar sahibinden iptal edilir.");
    }

    private static void ValidateDerivedField(
        bool? isEarnedIncome,
        IDictionary<string, List<string>> errors)
    {
        if (isEarnedIncome is not null)
        {
            AddError(
                errors,
                "isEarnedIncome",
                "isEarnedIncome istemci tarafından gönderilemez; kaynaktan türetilir.");
        }
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

    private static CashInflowSource? ParseSource(
        string? value,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParseSource(value, out var parsed))
        {
            AddError(errors, "source", "Geçerli bir para girişi kaynağı seçilmelidir.");
        }

        return parsed;
    }

    private static CashInflowStatus? ParseStatus(
        string? value,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParseStatus(value, out var parsed))
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
