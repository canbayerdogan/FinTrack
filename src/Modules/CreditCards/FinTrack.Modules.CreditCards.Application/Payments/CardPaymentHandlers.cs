using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Common;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;

namespace FinTrack.Modules.CreditCards.Application.Payments;

/// <summary>
/// Karta ödeme ekler (docs/05 §5.4). Ödeme gider değildir; yalnızca kart borcunu
/// azaltır. Kart kullanıcıya ait ve arşivlenmemiş olmalıdır; aksi halde 404/400.
/// </summary>
public sealed class CreateCardPaymentHandler(
    ICurrentUser currentUser,
    ICardRepository cards,
    ICardPaymentRepository payments,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CreateCardPaymentCommand, Result<CardPaymentDto>>
{
    public async Task<Result<CardPaymentDto>> HandleAsync(
        CreateCardPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var today = UserTime.Today(clock, currentUser.PreferredTimeZone);

        var card = await cards.FindAsync(userId, command.CardId, cancellationToken);
        if (card is null)
        {
            return Result.Failure<CardPaymentDto>(ApplicationErrors.CardNotFound());
        }

        if (card.IsArchived)
        {
            return Result.Failure<CardPaymentDto>(ApplicationErrors.CardArchived());
        }

        var createResult = CardPayment.Create(
            userId,
            command.CardId,
            command.Amount,
            command.Currency,
            command.PaidOn ?? default,
            command.Note,
            today,
            clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<CardPaymentDto>(createResult.Error);
        }

        var payment = createResult.Value;
        await payments.AddAsync(payment, cancellationToken);
        var outcome = await unitOfWork.SaveChangesAsync(cancellationToken);

        return outcome switch
        {
            SaveChangesOutcome.DuplicateKey => Result.Failure<CardPaymentDto>(ApplicationErrors.DuplicateKey()),
            SaveChangesOutcome.ConcurrencyConflict => Result.Failure<CardPaymentDto>(ApplicationErrors.Concurrency()),
            _ => Result.Success(CardPaymentMapper.ToDto(payment)),
        };
    }
}

/// <summary>
/// Kart ödemesini iptal eder (docs/05 §5.4). İptal gerekçesi zorunludur; iptal
/// borcu geri yükseltir ve hard delete yapılmaz.
/// </summary>
public sealed class CancelCardPaymentHandler(
    ICurrentUser currentUser,
    ICardPaymentRepository payments,
    IUnitOfWork unitOfWork,
    IClock clock)
    : ICommandHandler<CancelCardPaymentCommand, Result>
{
    public async Task<Result> HandleAsync(
        CancelCardPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var payment = await payments.FindAsync(userId, command.CardId, command.PaymentId, cancellationToken);
        if (payment is null)
        {
            return Result.Failure(ApplicationErrors.CardPaymentNotFound());
        }

        if (!RowVersionCodec.TryParse(command.RowVersion, out var requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.InvalidRowVersion());
        }

        if (!RowVersionCodec.Matches(payment.RowVersion, requestedRowVersion))
        {
            return Result.Failure(ApplicationErrors.Concurrency());
        }

        var cancelResult = payment.Cancel(command.Reason, clock.UtcNow);
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
