using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Statements;

/// <summary>
/// Ekstre oluşturur (docs/05 §5.6). Ekstre yeni gider/borç kaynağı değildir.
/// Aynı kartta <b>aktif</b> ekstre dönemleri çakışamaz; iptal edilmiş ekstre
/// engel değildir. Çakışma kontrolü ile ekleme yarışabildiğinden işlem
/// Serializable'dır.
/// </summary>
public sealed class CreateStatementHandler(
    ICurrentUser currentUser,
    ICardRepository cards,
    IStatementRepository statements,
    StatementDerivation derivation,
    ITransactionScopeFactory transactions,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CreateStatementCommand, Result<StatementDto>>
{
    public async Task<Result<StatementDto>> HandleAsync(
        CreateStatementCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await cards.FindAsync(userId, command.CardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure<StatementDto>(ApplicationErrors.CardNotFound());
        }

        if (card.IsArchived)
        {
            return Result.Failure<StatementDto>(ApplicationErrors.CardArchived());
        }

        var createResult = Statement.Create(
            userId,
            command.CardId,
            command.PeriodStart ?? default,
            command.PeriodEnd ?? default,
            command.DueDate ?? default,
            command.StatementAmount,
            command.Currency,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<StatementDto>(createResult.Error);
        }

        var statement = createResult.Value;

        await using var transaction = await transactions.BeginSerializableAsync(cancellationToken);

        var overlaps = await statements.HasOverlappingActiveStatementAsync(
            userId,
            command.CardId,
            statement.PeriodStart,
            statement.PeriodEnd,
            cancellationToken);
        if (overlaps)
        {
            return Result.Failure<StatementDto>(ApplicationErrors.StatementPeriodOverlap());
        }

        await statements.AddAsync(statement, cancellationToken);
        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure<StatementDto>(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<StatementDto>(ApplicationErrors.DuplicateKey());
        }

        await transaction.CommitAsync(cancellationToken);

        var dto = await derivation.ToDtoAsync(
            userId,
            StatementMapper.ToRecord(statement),
            currentUser.PreferredTimeZone,
            cancellationToken);

        return Result.Success(dto);
    }
}

/// <summary>
/// Ekstreyi iptal eder (docs/05 §5.6). İptal gerekçesi zorunludur; iptal borç
/// hesabını değiştirmez, dönem çakışması engelini kaldırır ve hard delete yapmaz.
/// </summary>
public sealed class CancelStatementHandler(
    ICurrentUser currentUser,
    IStatementRepository statements,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CancelStatementCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelStatementCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var statement = await statements.FindAsync(userId, command.CardId, command.StatementId, cancellationToken);
        if (statement is null)
        {
            return Result.Failure(ApplicationErrors.StatementNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(statement.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = statement.Cancel(command.Reason, clock.UtcNow);
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
