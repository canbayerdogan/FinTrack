using FinTrack.Modules.CreditCards.Application.Abstractions;
using FinTrack.Modules.CreditCards.Application.Cards;

namespace FinTrack.Modules.CreditCards.Application.Statements;

/// <summary>
/// Ekstrelerin türetilen alanlarını okuma anında hesaplar (docs/05 §5.6).
/// <list type="bullet">
/// <item><c>ComputedDebtAtPeriodEnd</c>: <c>PeriodEnd</c> gününün sonu
/// kesitiyle kart borcu (<see cref="CardDebtService"/>).</item>
/// <item><c>CarriedOverAmount</c>: önceki aktif ekstre varsa
/// <c>max(0, önceki.StatementAmount − (önceki.PeriodEnd, mevcut.PeriodEnd]
/// aralığındaki kesitte aktif ödemeler)</c>; yoksa <c>0</c>.</item>
/// </list>
/// </summary>
public sealed class StatementDerivation(
    CardDebtService debtService,
    ICardPaymentQueries paymentQueries,
    IStatementQueries statementQueries)
{
    public async Task<StatementDto> ToDtoAsync(
        Guid userId,
        StatementRecord record,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var components = await debtService.ComputeAsync(
            userId,
            record.CardId,
            record.PeriodEnd,
            timeZoneId,
            cancellationToken);

        var carriedOver = await ComputeCarriedOverAsync(userId, record, timeZoneId, cancellationToken);

        return StatementMapper.ToDto(record, carriedOver, components.Debt);
    }

    private async Task<decimal> ComputeCarriedOverAsync(
        Guid userId,
        StatementRecord record,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var previous = await statementQueries.FindPreviousActiveAsync(
            userId,
            record.CardId,
            record.PeriodEnd,
            cancellationToken);
        if (previous is null)
        {
            return 0m;
        }

        // Alt sınır (önceki.PeriodEnd) hariç, üst sınır (mevcut.PeriodEnd) dahil.
        // İptal aktifliği mevcut ekstrenin kesitinde değerlendirilir.
        var payments = await paymentQueries.SumActivePaymentsAsync(
            userId,
            record.CardId,
            paidAfterExclusive: previous.PeriodEnd,
            asOfDate: record.PeriodEnd,
            timeZoneId,
            cancellationToken);

        var carriedOver = previous.StatementAmount - payments;
        return carriedOver > 0m ? carriedOver : 0m;
    }
}
