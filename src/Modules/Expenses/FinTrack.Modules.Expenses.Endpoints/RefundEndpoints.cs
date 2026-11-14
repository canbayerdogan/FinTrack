using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinTrack.Modules.Expenses.Endpoints;

internal static class RefundEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/refunds", async (
                Guid id,
                IQueryHandler<ListRefundsQuery, Result<IReadOnlyList<RefundDto>>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(new ListRefundsQuery(id), cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("ListRefunds")
            .WithSummary("Harcamanın iadelerini listeler.");

        group.MapPost("/{id:guid}/refunds", async (
                Guid id,
                AddRefundRequest request,
                ICommandHandler<AddRefundCommand, Result<RefundDto>> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new AddRefundCommand(
                        id,
                        request.Amount,
                        request.Currency,
                        request.RefundedOn,
                        request.Description),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created(
                        $"{ExpensesModule.RoutePrefix}/{id}/refunds/{result.Value.Id}",
                        result.Value)
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("AddRefund")
            .WithSummary("Harcamaya iade ekler; gelir oluşturmaz.");

        group.MapPost("/{id:guid}/refunds/{refundId:guid}/cancel", async (
                Guid id,
                Guid refundId,
                CancelRequest request,
                ICommandHandler<CancelRefundCommand, Result> handler,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(
                    new CancelRefundCommand(id, refundId, request.Reason, request.RowVersion),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.NoContent()
                    : ExpensesProblems.FromError(context, result.Error);
            })
            .WithName("CancelRefund")
            .WithSummary("İadeyi iptal eder; net tutar geri yükselir.");
    }
}
