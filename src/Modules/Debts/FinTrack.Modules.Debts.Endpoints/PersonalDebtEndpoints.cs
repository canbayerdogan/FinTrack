using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.Debts.Endpoints;

internal static class PersonalDebtEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (
                string? status,
                bool? includeCancelled,
                int? page,
                int? pageSize,
                IQueryHandler<ListPersonalDebtsQuery, Result<PagedResult<PersonalDebtDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                PersonalDebtStatus? parsedStatus = null;

                if (!RequestParsing.TryParsePersonalDebtStatus(status, out parsedStatus))
                {
                    AddError(errors, "status", "Geçerli bir durum seçilmelidir.");
                }

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
                    return DebtsProblems.Validation(context, ToErrors(errors));
                }

                var filter = new PersonalDebtFilter(
                    parsedStatus,
                    includeCancelled ?? false,
                    resolvedPage,
                    resolvedPageSize);

                var result = await handler.HandleAsync(
                    new ListPersonalDebtsQuery(filter),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("ListPersonalDebts")
            .WithSummary("Kişisel borçları sayfalı listeler; varsayılan yalnızca aktif borçlardır.");

        group.MapGet("/{id:guid}", async (
                Guid id,
                IQueryHandler<GetPersonalDebtQuery, Result<PersonalDebtDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetPersonalDebtQuery(id), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("GetPersonalDebt")
            .WithSummary("Kişisel borcu geri ödemeleriyle getirir.");

        group.MapPost("/", async (
                CreatePersonalDebtRequest request,
                ICommandHandler<CreatePersonalDebtCommand, Result<PersonalDebtDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreatePersonalDebtCommand(
                        request.CounterpartyName,
                        request.Principal,
                        request.BorrowedOn,
                        request.DueDate,
                        request.Description,
                        request.Currency),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created(
                        $"{DebtsModule.PersonalDebtRoutePrefix}/{result.Value.Id}",
                        result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("CreatePersonalDebt")
            .WithSummary("Kişisel borcu ve bağlı para girişini aynı işlemde oluşturur.");

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelRequest request,
                ICommandHandler<CancelPersonalDebtCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelPersonalDebtCommand(id, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("CancelPersonalDebt")
            .WithSummary("Kişisel borcu ve bağlı para girişini iptal eder.");

        group.MapGet("/{id:guid}/repayments", async (
                Guid id,
                IQueryHandler<ListRepaymentsQuery, Result<IReadOnlyList<RepaymentDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new ListRepaymentsQuery(id), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("ListRepayments")
            .WithSummary("Kişisel borca ait geri ödemeleri listeler.");

        group.MapPost("/{id:guid}/repayments", async (
                Guid id,
                AddRepaymentRequest request,
                ICommandHandler<AddRepaymentCommand, Result<RepaymentDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new AddRepaymentCommand(id, request.Amount, request.PaidOn, request.Note),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created(
                        $"{DebtsModule.PersonalDebtRoutePrefix}/{id}/repayments/{result.Value.Id}",
                        result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("AddRepayment")
            .WithSummary("Kişisel borca geri ödeme ekler; kalan tutar yeniden hesaplanır.");

        group.MapPost("/{id:guid}/repayments/{repaymentId:guid}/cancel", async (
                Guid id,
                Guid repaymentId,
                CancelRequest request,
                ICommandHandler<CancelRepaymentCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelRepaymentCommand(id, repaymentId, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("CancelRepayment")
            .WithSummary("Geri ödemeyi iptal eder; kalan borç geri yükselir.");
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
