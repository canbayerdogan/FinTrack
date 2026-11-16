using FinTrack.Modules.CashInflows.Application.Abstractions;
using FinTrack.Modules.CashInflows.Application.Common;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CashInflows.Application.CashInflows;

public sealed class CreateCashInflowHandler(
    ICurrentUser currentUser,
    ICashInflowRepository cashInflows,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CreateCashInflowCommand, Result<CashInflowDto>>
{
    public async Task<Result<CashInflowDto>> HandleAsync(
        CreateCashInflowCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        if (command.Source is null)
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.CashInflowSourceRequired());
        }

        var createResult = CashInflow.Create(
            userId,
            command.Amount,
            command.Currency,
            command.OccurredOn ?? default,
            command.Source.Value,
            command.Description,
            today,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<CashInflowDto>(createResult.Error);
        }

        var inflow = createResult.Value;
        await cashInflows.AddAsync(inflow, cancellationToken);
        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome switch
        {
            SaveChangesOutcome.DuplicateKey => Result.Failure<CashInflowDto>(ApplicationErrors.DuplicateKey()),
            SaveChangesOutcome.ConcurrencyConflict => Result.Failure<CashInflowDto>(ApplicationErrors.Concurrency()),
            _ => Result.Success(CashInflowMapper.ToDto(inflow)),
        };
    }
}

public sealed class UpdateCashInflowHandler(
    ICurrentUser currentUser,
    ICashInflowRepository cashInflows,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<UpdateCashInflowCommand, Result<CashInflowDto>>
{
    public async Task<Result<CashInflowDto>> HandleAsync(
        UpdateCashInflowCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        var inflow = await cashInflows.FindAsync(userId, command.Id, cancellationToken);
        if (inflow is null)
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.CashInflowNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(inflow.RowVersion, requestedRowVersion))
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.Concurrency());
        }

        if (command.Source is null)
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.CashInflowSourceRequired());
        }

        var updateResult = inflow.Update(
            command.Amount,
            command.Currency,
            command.OccurredOn ?? default,
            command.Source.Value,
            command.Description,
            today);
        if (updateResult.IsFailure)
        {
            return Result.Failure<CashInflowDto>(updateResult.Error);
        }

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<CashInflowDto>(ApplicationErrors.DuplicateKey());
        }

        return Result.Success(CashInflowMapper.ToDto(inflow));
    }
}

public sealed class CancelCashInflowHandler(
    ICurrentUser currentUser,
    ICashInflowRepository cashInflows,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CancelCashInflowCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelCashInflowCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var inflow = await cashInflows.FindAsync(userId, command.Id, cancellationToken);
        if (inflow is null)
        {
            return Result.Failure(ApplicationErrors.CashInflowNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(inflow.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = inflow.Cancel(command.Reason, clock.UtcNow);
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

        return Result.Success();
    }
}
