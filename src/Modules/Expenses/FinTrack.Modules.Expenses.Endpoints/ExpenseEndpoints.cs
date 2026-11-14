using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.Expenses.Endpoints;

internal static class ExpenseEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("", async (
                string? from,
                string? to,
                string? categoryId,
                string? cardId,
                string? paymentMethod,
                string? status,
                bool? includeCancelled,
                int? page,
                int? pageSize,
                IQueryHandler<ListExpensesQuery, Result<PagedResult<ExpenseDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                var parsedFrom = ParseDate(from, "from", errors);
                var parsedTo = ParseDate(to, "to", errors);
                var parsedCategoryId = ParseGuid(categoryId, "categoryId", errors);
                var parsedCardId = ParseGuid(cardId, "cardId", errors);
                var parsedPaymentMethod = ParsePaymentMethod(paymentMethod, errors);
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
                    return ExpensesProblems.Validation(context, ToErrors(errors));
                }

                var filter = new ExpenseFilter(
                    parsedFrom,
                    parsedTo,
                    parsedCategoryId,
                    parsedCardId,
                    parsedPaymentMethod,
                    parsedStatus,
                    includeCancelled ?? false,
                    resolvedPage,
                    resolvedPageSize);

                var result = await handler.HandleAsync(new ListExpensesQuery(filter), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("ListExpenses")
            .WithSummary("Harcamaları filtreli ve sayfalı listeler.");

        group.MapGet("/{id:guid}", async (
                Guid id,
                IQueryHandler<GetExpenseQuery, Result<ExpenseDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetExpenseQuery(id), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("GetExpense")
            .WithSummary("Tek harcamayı getirir.");

        group.MapPost("", async (
                CreateExpenseRequest request,
                ICommandHandler<CreateExpenseCommand, Result<ExpenseDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var paymentMethod = ParsePaymentMethod(request.PaymentMethod, errors);

                if (errors.Count > 0)
                {
                    return ExpensesProblems.Validation(context, ToErrors(errors));
                }

                var result = await handler.HandleAsync(
                    new CreateExpenseCommand(
                        request.Amount,
                        request.Currency,
                        request.OccurredOn,
                        request.CategoryId,
                        request.Description,
                        paymentMethod,
                        request.CardId),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"{ExpensesModule.RoutePrefix}/{result.Value.Id}", result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("CreateExpense")
            .WithSummary("Harcama oluşturur; kart alışverişi de tek kayıttır.");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateExpenseRequest request,
                ICommandHandler<UpdateExpenseCommand, Result<ExpenseDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                if (request.RecordedAtUtc is not null)
                {
                    AddError(errors, "recordedAtUtc", "Oluşturulma zamanı değiştirilemez.");
                }

                var paymentMethod = ParsePaymentMethod(request.PaymentMethod, errors);

                if (errors.Count > 0)
                {
                    return ExpensesProblems.Validation(context, ToErrors(errors));
                }

                var result = await handler.HandleAsync(
                    new UpdateExpenseCommand(
                        id,
                        request.Amount,
                        request.Currency,
                        request.OccurredOn,
                        request.CategoryId,
                        request.Description,
                        paymentMethod,
                        request.CardId,
                        request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("UpdateExpense")
            .WithSummary("Harcamayı günceller.");

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelRequest request,
                ICommandHandler<CancelExpenseCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelExpenseCommand(id, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("CancelExpense")
            .WithSummary("Harcamayı iptal eder; hard delete yoktur.");
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

    private static Guid? ParseGuid(
        string? value,
        string field,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParseGuid(value, out var parsed))
        {
            AddError(errors, field, "Geçerli bir kimlik (GUID) olmalıdır.");
        }

        return parsed;
    }

    private static PaymentMethod? ParsePaymentMethod(
        string? value,
        IDictionary<string, List<string>> errors)
    {
        if (!RequestParsing.TryParsePaymentMethod(value, out var parsed))
        {
            AddError(errors, "paymentMethod", "Geçerli bir ödeme yöntemi seçilmelidir.");
        }

        return parsed;
    }

    private static ExpenseStatus? ParseStatus(
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
