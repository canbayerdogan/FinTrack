using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Cards;

public sealed class CreateCardHandler(
    ICurrentUser currentUser,
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CreateCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> HandleAsync(
        CreateCardCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var createResult = CreditCard.Create(
            userId,
            command.BankName,
            command.DisplayName,
            command.LogoKey,
            command.CreditLimit,
            command.StatementDay,
            command.PaymentDueDay,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<CardDto>(createResult.Error);
        }

        var card = createResult.Value;
        await cards.AddAsync(card, cancellationToken);
        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome switch
        {
            SaveChangesOutcome.DuplicateKey => Result.Failure<CardDto>(ApplicationErrors.DuplicateKey()),
            SaveChangesOutcome.ConcurrencyConflict => Result.Failure<CardDto>(ApplicationErrors.Concurrency()),
            _ => Result.Success(CardMapper.ToDto(card)),
        };
    }
}

public sealed class UpdateCardHandler(
    ICurrentUser currentUser,
    ICardRepository cards,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCardCommand, Result<CardDto>>
{
    public async Task<Result<CardDto>> HandleAsync(
        UpdateCardCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await cards.FindAsync(userId, command.Id, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardDto>(ApplicationErrors.CardNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure<CardDto>(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(card.RowVersion, requestedRowVersion))
        {
            return Result.Failure<CardDto>(ApplicationErrors.Concurrency());
        }

        var updateResult = card.Update(
            command.BankName,
            command.DisplayName,
            command.LogoKey,
            command.CreditLimit,
            command.StatementDay,
            command.PaymentDueDay);
        if (updateResult.IsFailure)
        {
            return Result.Failure<CardDto>(updateResult.Error);
        }

        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (outcome == SaveChangesOutcome.ConcurrencyConflict)
        {
            return Result.Failure<CardDto>(ApplicationErrors.Concurrency());
        }

        if (outcome == SaveChangesOutcome.DuplicateKey)
        {
            return Result.Failure<CardDto>(ApplicationErrors.DuplicateKey());
        }

        return Result.Success(CardMapper.ToDto(card));
    }
}

public sealed class ArchiveCardHandler(
    ICurrentUser currentUser,
    ICardRepository cards,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<ArchiveCardCommand, Result>
{
    public async Task<Result> HandleAsync(ArchiveCardCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var card = await cards.FindAsync(userId, command.Id, cancellationToken);
        if (card is null)
        {
            return Result.Failure(ApplicationErrors.CardNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(card.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var archiveResult = card.Archive(clock.UtcNow);
        if (archiveResult.IsFailure)
        {
            return archiveResult;
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
