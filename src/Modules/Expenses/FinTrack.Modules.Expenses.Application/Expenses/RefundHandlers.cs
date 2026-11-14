using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Expenses;

public sealed class AddRefundHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IUnitOfWork unitOfWork,
    ITransactionScopeFactory transactions,
    IClock clock)
    : ICommandHandler<AddRefundCommand, Result<RefundDto>>
{
    public async Task<Result<RefundDto>> HandleAsync(
        AddRefundCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        // Aktif iade toplamının aşılmaması, aynı anda eklenen iadelerle
        // yarışabilir; kontrol ve yazma Serializable işlem içindedir.
        await using var transaction = await transactions.BeginSerializableAsync(cancellationToken);

        var expense = await expenses.FindAsync(userId, command.ExpenseId, cancellationToken);
        if (expense is null)
        {
            return Result.Failure<RefundDto>(ApplicationErrors.ExpenseNotFound());
        }

        var addResult = expense.AddRefund(
            command.Amount,
            command.Currency,
            command.RefundedOn ?? default,
            command.Description,
            today,
            clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure<RefundDto>(addResult.Error);
        }

        // Yeni iadeyi açıkça ekle; aksi halde EF anahtarı dolu olduğu için
        // varlığı var sayıp UPDATE üretir ve sahte eşzamanlılık hatası verir.
        await expenses.AddRefundAsync(addResult.Value, cancellationToken);

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome is SaveChangesOutcome.ConcurrencyConflict or SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<RefundDto>(
                outcome == SaveChangesOutcome.ConcurrencyConflict
                    ? ApplicationErrors.Concurrency()
                    : ApplicationErrors.DuplicateKey());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ExpenseMapper.ToDto(addResult.Value));
    }
}

public sealed class CancelRefundHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IUnitOfWork unitOfWork,
    ITransactionScopeFactory transactions,
    IClock clock)
    : ICommandHandler<CancelRefundCommand, Result>
{
    public async Task<Result> HandleAsync(CancelRefundCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        await using var transaction = await transactions.BeginSerializableAsync(cancellationToken);

        var expense = await expenses.FindAsync(userId, command.ExpenseId, cancellationToken);
        if (expense is null)
        {
            return Result.Failure(ApplicationErrors.ExpenseNotFound());
        }

        var refund = expense.Refunds.FirstOrDefault(candidate => candidate.Id == command.RefundId);
        if (refund is null)
        {
            return Result.Failure(ApplicationErrors.RefundNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(refund.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = expense.CancelRefund(command.RefundId, command.Reason, clock.UtcNow);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure(ApplicationErrors.DuplicateKey());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
