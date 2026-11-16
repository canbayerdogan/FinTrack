using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Domain;

public sealed class StatementDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 10, 26);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 25);
    private static readonly DateOnly DueDate = new(2026, 12, 5);
    private static readonly DateTime Now = new(2026, 11, 25, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_gecerli_ekstre_aktif_ve_alanlar_dogru()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var statement = result.Value;
        Assert.Equal(PeriodStart, statement.PeriodStart);
        Assert.Equal(PeriodEnd, statement.PeriodEnd);
        Assert.Equal(DueDate, statement.DueDate);
        Assert.Equal(6500m, statement.StatementAmount);
        Assert.Equal("TRY", statement.Currency);
        Assert.Equal(StatementStatus.Active, statement.Status);
        Assert.Equal(Now, statement.RecordedAtUtc);
    }

    [Fact]
    public void Create_donem_baslangici_bitisten_sonra_olamaz()
    {
        var result = Create(periodStart: PeriodEnd.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("StatementPeriodInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_son_odeme_tarihi_donem_bitisinden_once_olamaz()
    {
        var result = Create(dueDate: PeriodEnd.AddDays(-1));

        Assert.True(result.IsFailure);
        Assert.Equal("StatementDueDateBeforePeriodEnd", result.Error.Code);
    }

    [Fact]
    public void Create_tarihler_zorunludur()
    {
        Assert.Equal("StatementPeriodStartRequired", Create(periodStart: DateOnly.MinValue).Error.Code);
        Assert.Equal("StatementPeriodEndRequired", Create(periodEnd: DateOnly.MinValue).Error.Code);
        Assert.Equal("StatementDueDateRequired", Create(dueDate: DateOnly.MinValue).Error.Code);
    }

    [Fact]
    public void Create_negatif_tutar_reddedilir_sifir_kabul_edilir()
    {
        Assert.Equal("StatementAmountInvalid", Create(statementAmount: -1m).Error.Code);
        Assert.True(Create(statementAmount: 0m).IsSuccess);
    }

    [Fact]
    public void Create_desteklenmeyen_para_birimi_reddedilir()
    {
        var result = Create(currency: "EUR");

        Assert.True(result.IsFailure);
        Assert.Equal("StatementCurrencyUnsupported", result.Error.Code);
    }

    [Fact]
    public void Iptal_gerekcesiz_reddedilir_gerekceli_idempotenttir()
    {
        var statement = Create().Value;

        var missing = statement.Cancel("  ", Now);
        Assert.True(missing.IsFailure);
        Assert.Equal("CancellationReasonRequired", missing.Error.Code);
        Assert.Equal(StatementStatus.Active, statement.Status);

        var cancelled = statement.Cancel("Yanlış dönem", Now.AddMinutes(1));
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(StatementStatus.Cancelled, statement.Status);
        Assert.Equal(Now.AddMinutes(1), statement.CancelledAtUtc);

        var again = statement.Cancel("Başka", Now.AddHours(1));
        Assert.True(again.IsSuccess);
        Assert.Equal(Now.AddMinutes(1), statement.CancelledAtUtc);
    }

    private static Result<Statement> Create(
        Guid? userId = null,
        Guid? cardId = null,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null,
        DateOnly? dueDate = null,
        decimal statementAmount = 6500m,
        string? currency = "TRY") =>
        Statement.Create(
            userId ?? UserId,
            cardId ?? CardId,
            periodStart ?? PeriodStart,
            periodEnd ?? PeriodEnd,
            dueDate ?? DueDate,
            statementAmount,
            currency,
            Now);
}
