using FinTrack.Modules.Expenses.Application.Abstractions;
using FinTrack.Modules.Expenses.Application.Common;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.Expenses.Application.Expenses;

public sealed class CreateExpenseHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IUnitOfWork unitOfWork,
    ExpenseReferenceService references,
    IClock clock)
    : ICommandHandler<CreateExpenseCommand, Result<ExpenseDto>>
{
    public async Task<Result<ExpenseDto>> HandleAsync(
        CreateExpenseCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        // Eksik enum değeri domain'de reddedilir; Cash'e düşülmez.
        var paymentMethod = command.PaymentMethod ?? (PaymentMethod)(-1);

        var createResult = Expense.Create(
            userId,
            command.Amount,
            command.Currency,
            command.OccurredOn ?? default,
            command.CategoryId ?? Guid.Empty,
            command.Description,
            paymentMethod,
            command.CardId,
            today,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<ExpenseDto>(createResult.Error);
        }

        var expense = createResult.Value;

        var categoryResult = await references.ResolveCategoryAsync(
            userId,
            expense.CategoryId,
            currentCategoryId: null,
            cancellationToken);
        if (categoryResult.IsFailure)
        {
            return Result.Failure<ExpenseDto>(categoryResult.Error);
        }

        var cardResult = await references.ResolveCardAsync(
            userId,
            expense.PaymentMethod,
            expense.CardId,
            currentCardId: null,
            cancellationToken);
        if (cardResult.IsFailure)
        {
            return Result.Failure<ExpenseDto>(cardResult.Error);
        }

        await expenses.AddAsync(expense, cancellationToken);
        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome switch
        {
            SaveChangesOutcome.DuplicateKey => Result.Failure<ExpenseDto>(ApplicationErrors.DuplicateKey()),
            SaveChangesOutcome.ConcurrencyConflict => Result.Failure<ExpenseDto>(ApplicationErrors.Concurrency()),
            _ => Result.Success(ExpenseMapper.ToDto(expense, categoryResult.Value.Name)),
        };
    }
}

public sealed class UpdateExpenseHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IUnitOfWork unitOfWork,
    ITransactionScopeFactory transactions,
    ExpenseReferenceService references,
    IClock clock)
    : ICommandHandler<UpdateExpenseCommand, Result<ExpenseDto>>
{
    public async Task<Result<ExpenseDto>> HandleAsync(
        UpdateExpenseCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        // Tutar düşürme aktif iade toplamıyla yarışabilir; okuma ve yazma aynı
        // Serializable işlem içinde yapılır.
        await using var transaction = await transactions.BeginSerializableAsync(cancellationToken);

        var expense = await expenses.FindAsync(userId, command.Id, cancellationToken);
        if (expense is null)
        {
            return Result.Failure<ExpenseDto>(ApplicationErrors.ExpenseNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure<ExpenseDto>(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(expense.RowVersion, requestedRowVersion))
        {
            return Result.Failure<ExpenseDto>(ApplicationErrors.Concurrency());
        }

        var paymentMethod = command.PaymentMethod ?? (PaymentMethod)(-1);
        var currentCategoryId = expense.CategoryId;
        var currentCardId = expense.CardId;

        var updateResult = expense.Update(
            command.Amount,
            command.Currency,
            command.OccurredOn ?? default,
            command.CategoryId ?? Guid.Empty,
            command.Description,
            paymentMethod,
            command.CardId,
            today);
        if (updateResult.IsFailure)
        {
            return Result.Failure<ExpenseDto>(updateResult.Error);
        }

        var categoryResult = await references.ResolveCategoryAsync(
            userId,
            expense.CategoryId,
            currentCategoryId,
            cancellationToken);
        if (categoryResult.IsFailure)
        {
            return Result.Failure<ExpenseDto>(categoryResult.Error);
        }

        var cardResult = await references.ResolveCardAsync(
            userId,
            expense.PaymentMethod,
            expense.CardId,
            currentCardId,
            cancellationToken);
        if (cardResult.IsFailure)
        {
            return Result.Failure<ExpenseDto>(cardResult.Error);
        }

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure<ExpenseDto>(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<ExpenseDto>(ApplicationErrors.DuplicateKey());
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ExpenseMapper.ToDto(expense, categoryResult.Value.Name));
    }
}

public sealed class CancelExpenseHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IUnitOfWork unitOfWork,
    ITransactionScopeFactory transactions,
    IClock clock)
    : ICommandHandler<CancelExpenseCommand, Result>
{
    public async Task<Result> HandleAsync(CancelExpenseCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        // Aktif iade kontrolü ile iade ekleme yarışabilir; ikisi de Serializable.
        await using var transaction = await transactions.BeginSerializableAsync(cancellationToken);

        var expense = await expenses.FindAsync(userId, command.Id, cancellationToken);
        if (expense is null)
        {
            return Result.Failure(ApplicationErrors.ExpenseNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(expense.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = expense.Cancel(command.Reason, clock.UtcNow);
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
