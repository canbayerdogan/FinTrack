using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.CashAdvances;

/// <summary>
/// Nakit avansı kart borcuna ekler ve aynı kullanıcı işleminde bağlı para
/// girişini oluşturur (docs/05 §5.5, docs/06 §6.5). İki modülün yazması
/// ADR-0009 ile paylaşılan transaction'da yapılır; commit yalnızca handler'dadır.
/// </summary>
public sealed class CreateCashAdvanceHandler(
    ICurrentUser currentUser,
    ICardRepository cards,
    ICashAdvanceRepository advances,
    IUnitOfWork unitOfWork,
    IWriteTransactionScopeFactory transactions,
    IDebtCashInflowWriter inflowWriter,
    IClock clock)
    : ICommandHandler<CreateCashAdvanceCommand, Result<CashAdvanceDto>>
{
    public async Task<Result<CashAdvanceDto>> HandleAsync(
        CreateCashAdvanceCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        var card = await cards.FindAsync(userId, command.CardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CashAdvanceDto>(ApplicationErrors.CardNotFound());
        }

        if (card.IsArchived)
        {
            return Result.Failure<CashAdvanceDto>(ApplicationErrors.CardArchived());
        }

        var createResult = CashAdvance.Create(
            userId,
            command.CardId,
            command.Amount,
            command.Currency,
            command.TakenOn ?? default,
            command.Description,
            today,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<CashAdvanceDto>(createResult.Error);
        }

        var advance = createResult.Value;
        var scope = await transactions.BeginAsync(cancellationToken);

        try
        {
            unitOfWork.Enlist(scope);
            await advances.AddAsync(advance, cancellationToken);

            var inflowResult = await inflowWriter.CreateAsync(
                new DebtInflowCreate(
                    userId,
                    advance.Amount,
                    advance.Currency,
                    advance.TakenOn,
                    DebtInflowKind.CashAdvance,
                    advance.Id,
                    advance.Description,
                    today,
                    clock.UtcNow),
                scope,
                cancellationToken);
            if (inflowResult.IsFailure)
            {
                return Result.Failure<CashAdvanceDto>(inflowResult.Error);
            }

            var linkResult = advance.LinkCashInflow(inflowResult.Value);
            if (linkResult.IsFailure)
            {
                return Result.Failure<CashAdvanceDto>(linkResult.Error);
            }

            var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (outcome == SaveChangesOutcome.ConcurrencyConflict)
            {
                return Result.Failure<CashAdvanceDto>(ApplicationErrors.Concurrency());
            }

            if (outcome == SaveChangesOutcome.DuplicateKey)
            {
                return Result.Failure<CashAdvanceDto>(ApplicationErrors.DuplicateKey());
            }

            await scope.CommitAsync(cancellationToken);

            return Result.Success(CashAdvanceMapper.ToDto(advance));
        }
        finally
        {
            unitOfWork.Enlist(null);
            await scope.DisposeAsync();
        }
    }
}

/// <summary>
/// Nakit avansı ve bağlı para girişini aynı transaction'da iptal eder
/// (docs/05 §5.5). Kart ödemeleri geri alınmaz; borç yeniden hesaplanır.
/// </summary>
public sealed class CancelCashAdvanceHandler(
    ICurrentUser currentUser,
    ICashAdvanceRepository advances,
    IUnitOfWork unitOfWork,
    IWriteTransactionScopeFactory transactions,
    IDebtCashInflowWriter inflowWriter,
    IClock clock)
    : ICommandHandler<CancelCashAdvanceCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelCashAdvanceCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var advance = await advances.FindAsync(userId, command.CardId, command.CashAdvanceId, cancellationToken);
        if (advance is null)
        {
            return Result.Failure(ApplicationErrors.CashAdvanceNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(advance.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        if (advance.LinkedCashInflowId is null)
        {
            return Result.Failure(ApplicationErrors.CashAdvanceInflowMissing());
        }

        var scope = await transactions.BeginAsync(cancellationToken);

        try
        {
            unitOfWork.Enlist(scope);

            var cancelResult = advance.Cancel(command.Reason, clock.UtcNow);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }

            var inflowResult = await inflowWriter.CancelAsync(
                new DebtInflowCancel(
                    userId,
                    advance.LinkedCashInflowId.Value,
                    DebtInflowKind.CashAdvance,
                    advance.Id,
                    command.Reason!,
                    clock.UtcNow),
                scope,
                cancellationToken);
            if (inflowResult.IsFailure)
            {
                return inflowResult;
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

            await scope.CommitAsync(cancellationToken);

            return Result.Success();
        }
        finally
        {
            unitOfWork.Enlist(null);
            await scope.DisposeAsync();
        }
    }
}
