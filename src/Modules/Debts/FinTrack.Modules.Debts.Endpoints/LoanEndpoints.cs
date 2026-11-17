using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Domain;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.Debts.Endpoints;

internal static class LoanEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (
                string? status,
                bool? includeCancelled,
                int? page,
                int? pageSize,
                IQueryHandler<ListLoansQuery, Result<PagedResult<LoanDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                LoanStatus? parsedStatus = null;

                if (!RequestParsing.TryParseLoanStatus(status, out parsedStatus))
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

                var filter = new LoanFilter(
                    parsedStatus,
                    includeCancelled ?? false,
                    resolvedPage,
                    resolvedPageSize);

                var result = await handler.HandleAsync(
                    new ListLoansQuery(filter),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("ListLoans")
            .WithSummary("Kredileri sayfalı listeler; varsayılan yalnızca aktif kredilerdir.");

        group.MapGet("/{id:guid}", async (
                Guid id,
                IQueryHandler<GetLoanQuery, Result<LoanDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new GetLoanQuery(id), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("GetLoan")
            .WithSummary("Krediyi tam taksit planıyla getirir.");

        group.MapPost("/", async (
                CreateLoanRequest request,
                ICommandHandler<CreateLoanCommand, Result<LoanDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CreateLoanCommand(
                        request.Principal,
                        request.MonthlyPayment,
                        request.InstallmentCount,
                        request.FirstDueDate,
                        request.BankName,
                        request.Description,
                        request.Currency),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"{DebtsModule.LoanRoutePrefix}/{result.Value.Id}", result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("CreateLoan")
            .WithSummary("Krediyi ve bağlı para girişini aynı işlemde oluşturur.");

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                CancelRequest request,
                ICommandHandler<CancelLoanCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelLoanCommand(id, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("CancelLoan")
            .WithSummary("Krediyi ve bağlı para girişini iptal eder.");

        group.MapPost("/{id:guid}/installments/{number:int}/pay", async (
                Guid id,
                int number,
                PayInstallmentRequest request,
                ICommandHandler<PayInstallmentCommand, Result<LoanDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new PayInstallmentCommand(id, number, request.PaidOn, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("PayInstallment")
            .WithSummary("Sıradaki taksiti tam tutarıyla öder ve güncel krediyi döner.");

        group.MapPost("/{id:guid}/installments/{number:int}/payment/cancel", async (
                Guid id,
                int number,
                CancelRequest request,
                IQueryHandler<GetLoanQuery, Result<LoanDto>> getLoan,
                ICommandHandler<CancelInstallmentPaymentCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var loanResult = await getLoan.HandleAsync(new GetLoanQuery(id), cancellationToken);
                if (loanResult.IsFailure)
                {
                    return DebtsProblems.FromError(context, loanResult.Error);
                }

                var paymentId = loanResult.Value.Installments
                    .FirstOrDefault(installment => installment.Number == number)
                    ?.Payment?.Id;
                if (paymentId is null)
                {
                    return Results.NotFound();
                }

                var result = await handler.HandleAsync(
                    new CancelInstallmentPaymentCommand(
                        id,
                        number,
                        paymentId.Value,
                        request.Reason,
                        request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : DebtsProblems.FromError(context, result.Error);
            })
            .WithName("CancelInstallmentPayment")
            .WithSummary("Ödenmiş taksitin ödemesini iptal eder; taksit yeniden Planned olur.");
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
