using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Domain;

public sealed class CashAdvanceDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly DateOnly TakenOn = Today.AddDays(-3);
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_gecerli_avans_aktif_ve_alanlar_dogru()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var advance = result.Value;
        Assert.Equal(3000m, advance.Amount);
        Assert.Equal("TRY", advance.Currency);
        Assert.Equal(TakenOn, advance.TakenOn);
        Assert.Equal("Acil nakit", advance.Description);
        Assert.Equal(CashAdvanceStatus.Active, advance.Status);
        Assert.Null(advance.LinkedCashInflowId);
        Assert.Null(advance.CancelledAtUtc);
        Assert.Equal(Now, advance.RecordedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_sifir_veya_negatif_tutar_reddedilir(decimal amount)
    {
        var result = Create(amount: amount);

        Assert.True(result.IsFailure);
        Assert.Equal("CashAdvanceAmountInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_dort_ondaliktan_fazla_basamak_reddedilir()
    {
        var result = Create(amount: 10.12345m);

        Assert.True(result.IsFailure);
        Assert.Equal("CashAdvanceAmountScale", result.Error.Code);
    }

    [Fact]
    public void Create_desteklenmeyen_para_birimi_reddedilir()
    {
        var result = Create(currency: "USD");

        Assert.True(result.IsFailure);
        Assert.Equal("CashAdvanceCurrencyUnsupported", result.Error.Code);
    }

    [Fact]
    public void Create_gelecek_tarihli_avans_reddedilir()
    {
        var result = Create(takenOn: Today.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("CashAdvanceDateInFuture", result.Error.Code);
    }

    [Fact]
    public void Create_tarih_zorunludur()
    {
        var result = Create(takenOn: DateOnly.MinValue);

        Assert.True(result.IsFailure);
        Assert.Equal("CashAdvanceDateRequired", result.Error.Code);
    }

    [Fact]
    public void Create_uzun_aciklama_reddedilir()
    {
        var result = Create(description: new string('a', CashAdvance.DescriptionMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("CashAdvanceDescriptionTooLong", result.Error.Code);
    }

    [Fact]
    public void LinkCashInflow_bos_kimlik_ve_ikinci_baglanti_reddedilir()
    {
        var advance = Create().Value;

        var empty = advance.LinkCashInflow(Guid.Empty);
        Assert.True(empty.IsFailure);
        Assert.Equal("CashAdvanceInflowRequired", empty.Error.Code);

        var inflowId = Guid.NewGuid();
        Assert.True(advance.LinkCashInflow(inflowId).IsSuccess);
        Assert.Equal(inflowId, advance.LinkedCashInflowId);

        var again = advance.LinkCashInflow(Guid.NewGuid());
        Assert.True(again.IsFailure);
        Assert.Equal("StateConflict", again.Error.Code);
        Assert.Equal(inflowId, advance.LinkedCashInflowId);
    }

    [Fact]
    public void Iptal_gerekcesiz_reddedilir_gerekceli_idempotenttir()
    {
        var advance = Create().Value;

        var missing = advance.Cancel(null, Now.AddMinutes(1));
        Assert.True(missing.IsFailure);
        Assert.Equal("CancellationReasonRequired", missing.Error.Code);
        Assert.Equal(CashAdvanceStatus.Active, advance.Status);

        var cancelled = advance.Cancel("İptal edildi", Now.AddMinutes(2));
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(CashAdvanceStatus.Cancelled, advance.Status);
        Assert.Equal(Now.AddMinutes(2), advance.CancelledAtUtc);
        Assert.Equal("İptal edildi", advance.CancellationReason);
        Assert.Equal(TakenOn, advance.TakenOn);

        var again = advance.Cancel("Başka gerekçe", Now.AddHours(1));
        Assert.True(again.IsSuccess);
        Assert.Equal(Now.AddMinutes(2), advance.CancelledAtUtc);
    }

    private static Result<CashAdvance> Create(
        Guid? userId = null,
        Guid? cardId = null,
        decimal amount = 3000m,
        string? currency = "TRY",
        DateOnly? takenOn = null,
        string? description = "Acil nakit") =>
        CashAdvance.Create(
            userId ?? UserId,
            cardId ?? CardId,
            amount,
            currency,
            takenOn ?? TakenOn,
            description,
            Today,
            Now);
}
