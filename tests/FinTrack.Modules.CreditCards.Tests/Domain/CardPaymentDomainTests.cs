using FinTrack.Modules.CreditCards.Domain;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Domain;

public sealed class CardPaymentDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CardId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateOnly PaidOn = Today.AddDays(-2);
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_gecerli_odeme_aktif_ve_alanlar_dogru()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var payment = result.Value;
        Assert.Equal(500m, payment.Amount);
        Assert.Equal("TRY", payment.Currency);
        Assert.Equal(PaidOn, payment.PaidOn);
        Assert.Equal("Ekstre ödemesi", payment.Note);
        Assert.Equal(PaymentStatus.Active, payment.Status);
        Assert.Null(payment.CancelledAtUtc);
        Assert.Equal(Now, payment.RecordedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_sifir_veya_negatif_tutar_reddedilir(decimal amount)
    {
        var result = Create(amount: amount);

        Assert.True(result.IsFailure);
        Assert.Equal("CardPaymentAmountInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_dort_ondaliktan_fazla_basamak_reddedilir()
    {
        var result = Create(amount: 10.12345m);

        Assert.True(result.IsFailure);
        Assert.Equal("CardPaymentAmountScale", result.Error.Code);
    }

    [Fact]
    public void Create_desteklenmeyen_para_birimi_reddedilir()
    {
        var result = Create(currency: "USD");

        Assert.True(result.IsFailure);
        Assert.Equal("CardPaymentCurrencyUnsupported", result.Error.Code);
    }

    [Fact]
    public void Create_gelecek_tarihli_odeme_reddedilir()
    {
        var result = Create(paidOn: Today.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("CardPaymentDateInFuture", result.Error.Code);
    }

    [Fact]
    public void Create_tarih_zorunludur()
    {
        var result = Create(paidOn: DateOnly.MinValue);

        Assert.True(result.IsFailure);
        Assert.Equal("CardPaymentDateRequired", result.Error.Code);
    }

    [Fact]
    public void Create_uzun_not_reddedilir()
    {
        var result = Create(note: new string('a', CardPayment.NoteMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("CardPaymentNoteTooLong", result.Error.Code);
    }

    [Fact]
    public void Create_kart_zorunludur()
    {
        var result = Create(cardId: Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("CardIdRequired", result.Error.Code);
    }

    [Fact]
    public void Iptal_gerekcesiz_reddedilir_gerekceli_idempotenttir()
    {
        var payment = Create().Value;

        var missing = payment.Cancel(null, Now.AddMinutes(1));
        Assert.True(missing.IsFailure);
        Assert.Equal("CancellationReasonRequired", missing.Error.Code);
        Assert.Equal(PaymentStatus.Active, payment.Status);

        var cancelled = payment.Cancel("Yanlış giriş", Now.AddMinutes(2));
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal(Now.AddMinutes(2), payment.CancelledAtUtc);
        Assert.Equal("Yanlış giriş", payment.CancellationReason);
        Assert.Equal(PaidOn, payment.PaidOn);

        // İkinci iptal hata üretmez ve zamanı değiştirmez.
        var again = payment.Cancel("Başka", Now.AddHours(1));
        Assert.True(again.IsSuccess);
        Assert.Equal(Now.AddMinutes(2), payment.CancelledAtUtc);
    }

    private static Result<CardPayment> Create(
        Guid? userId = null,
        Guid? cardId = null,
        decimal amount = 500m,
        string? currency = "TRY",
        DateOnly? paidOn = null,
        string? note = "Ekstre ödemesi") =>
        CardPayment.Create(
            userId ?? UserId,
            cardId ?? CardId,
            amount,
            currency,
            paidOn ?? PaidOn,
            note,
            Today,
            Now);
}
