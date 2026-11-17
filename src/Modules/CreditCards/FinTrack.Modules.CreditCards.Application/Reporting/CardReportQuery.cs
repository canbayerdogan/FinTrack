using FinTrack.ModuleContracts;
using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Application.Statements;
using FinTrack.Modules.CreditCards.Domain;

namespace FinTrack.Modules.CreditCards.Application.Reporting;

/// <summary>
/// Reporting sözleşmesinin CreditCards tarafı. Kart borcu tek hesaplama
/// noktasından (<see cref="CardDebtService"/>) alınır; ekstre tutarı borca
/// eklenmez, yalnızca son ödeme tarihi bilgisi olarak döner (docs/05 §7.4–7.5).
/// </summary>
public sealed class CardReportQuery(
    ICardQueries cards,
    IStatementQueries statements,
    CardDebtService debtService) : ICardReportQuery
{
    private const int PageSize = 200;

    public async Task<CardDebtReport> GetDebtAsync(
        Guid userId,
        DateOnly asOfDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var activeCards = await ListActiveCardsAsync(userId, cancellationToken);

        var entries = new List<CardDebtEntry>(activeCards.Count);
        foreach (var card in activeCards)
        {
            var components = await debtService.ComputeAsync(
                userId, card.Id, asOfDate, timeZoneId, cancellationToken);

            entries.Add(new CardDebtEntry(
                card.Id,
                card.DisplayName,
                components.Debt,
                components.Debt < 0m));
        }

        var cardCreditTotal = entries
            .Where(entry => entry.IsInCredit)
            .Sum(entry => -entry.Debt);

        return new CardDebtReport(entries, cardCreditTotal);
    }

    public async Task<IReadOnlyList<CardStatementDue>> GetStatementDuesAsync(
        Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var activeCards = await ListActiveCardsAsync(userId, cancellationToken);
        var dues = new List<CardStatementDue>();

        foreach (var card in activeCards)
        {
            var page = 1;
            while (true)
            {
                var result = await statements.ListAsync(
                    userId,
                    card.Id,
                    new StatementFilter(
                        Status: StatementStatus.Active,
                        IncludeCancelled: false,
                        Page: page,
                        PageSize: PageSize),
                    cancellationToken);

                dues.AddRange(result.Items
                    .Where(statement => statement.DueDate >= from && statement.DueDate <= to)
                    .Select(statement => new CardStatementDue(
                        statement.CardId,
                        statement.Id,
                        statement.DueDate,
                        statement.StatementAmount)));

                if (result.Items.Count == 0 || page >= result.TotalPages)
                {
                    break;
                }

                page++;
            }
        }

        return dues
            .OrderBy(due => due.DueDate)
            .ThenBy(due => due.CardId)
            .ToArray();
    }

    private Task<IReadOnlyList<CardDto>> ListActiveCardsAsync(
        Guid userId, CancellationToken cancellationToken) =>
        cards.ListAsync(userId, new CardFilter(CardStatus.Active), cancellationToken);
}
