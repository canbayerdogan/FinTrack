using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Modules.CreditCards.Infrastructure.Queries;

internal sealed class CardQueries(CreditCardsDbContext db) : ICardQueries
{
    public async Task<IReadOnlyList<CardDto>> ListAsync(
        Guid userId,
        CardFilter filter,
        CancellationToken cancellationToken)
    {
        var query = db.Cards
            .AsNoTracking()
            .Where(card => card.UserId == userId);

        // Varsayılan yalnızca aktif kartlardır; arşivliler açıkça istenir
        // (docs/06 §6.1, docs/09 §3.6).
        var status = filter.Status ?? CardStatus.Active;
        query = status == CardStatus.Archived
            ? query.Where(card => card.IsArchived)
            : query.Where(card => !card.IsArchived);

        var cards = await query
            .OrderByDescending(card => card.CreatedAtUtc)
            .ThenBy(card => card.DisplayName)
            .ToListAsync(cancellationToken);

        return cards.Select(CardMapper.ToDto).ToList();
    }

    public async Task<CardDto?> FindAsync(
        Guid userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        var card = await db.Cards
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.Id == cardId,
                cancellationToken);

        return card is null ? null : CardMapper.ToDto(card);
    }
}
