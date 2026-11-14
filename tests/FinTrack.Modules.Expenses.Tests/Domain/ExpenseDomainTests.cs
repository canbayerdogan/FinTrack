using FinTrack.Modules.Expenses.Domain;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Domain;

public sealed class ExpenseDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateOnly OccurredOn = Today.AddDays(-1);
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_gecerli_harcama_aktif_ve_net_tutar_dogru()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var expense = result.Value;
        Assert.Equal(100m, expense.Amount);
        Assert.Equal("TRY", expense.Currency);
        Assert.Equal(0m, expense.RefundedAmount);
        Assert.Equal(100m, expense.NetAmount);
        Assert.Equal(ExpenseStatus.Active, expense.Status);
        Assert.Equal(Now, expense.RecordedAtUtc);
        Assert.Null(expense.CardId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_sifir_veya_negatif_tutar_reddedilir(decimal amount)
    {
        var result = Create(amount: amount);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseAmountInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_dort_ondaliktan_fazla_basamak_reddedilir()
    {
        var result = Create(amount: 10.12345m);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseAmountScale", result.Error.Code);
    }

    [Fact]
    public void Create_asiri_buyuk_tutar_reddedilir()
    {
        var result = Create(amount: Expense.MaxAmount + 1m);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseAmountTooLarge", result.Error.Code);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Create_desteklenmeyen_para_birimi_reddedilir(string currency)
    {
        var result = Create(currency: currency);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCurrencyUnsupported", result.Error.Code);
    }

    [Fact]
    public void Create_para_birimi_verilmezse_try_ve_buyuk_harfe_normalize_edilir()
    {
        var result = Create(currency: "try");

        Assert.True(result.IsSuccess);
        Assert.Equal("TRY", result.Value.Currency);
    }

    [Fact]
    public void Create_gelecek_tarih_reddedilir()
    {
        var result = Create(occurredOn: Today.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseDateInFuture", result.Error.Code);
    }

    [Fact]
    public void Create_500_karakterden_uzun_aciklama_reddedilir()
    {
        var result = Create(description: new string('a', Expense.DescriptionMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseDescriptionTooLong", result.Error.Code);
    }

    [Fact]
    public void Create_gecersiz_odeme_yontemi_reddedilir()
    {
        var result = Create(paymentMethod: (PaymentMethod)(-1));

        Assert.True(result.IsFailure);
        Assert.Equal("ExpensePaymentMethodInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_kredi_karti_kart_zorunlu()
    {
        var result = Create(paymentMethod: PaymentMethod.CreditCard);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCardRequired", result.Error.Code);
    }

    [Fact]
    public void Create_kredi_karti_disi_odeme_yontemi_kart_kabul_etmez()
    {
        var result = Create(paymentMethod: PaymentMethod.Cash, cardId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCardNotAllowed", result.Error.Code);
    }

    [Fact]
    public void Create_kredi_karti_karti_korur()
    {
        var cardId = Guid.NewGuid();

        var result = Create(paymentMethod: PaymentMethod.CreditCard, cardId: cardId);

        Assert.True(result.IsSuccess);
        Assert.Equal(cardId, result.Value.CardId);
    }

    [Fact]
    public void Update_iptal_edilmis_harcamayi_reddeder()
    {
        var expense = Create().Value;
        expense.Cancel("Yanlış kayıt", Now);

        var result = expense.Update(120m, "TRY", OccurredOn, CategoryId, null, PaymentMethod.Cash, null, Today);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCancelled", result.Error.Code);
    }

    [Fact]
    public void Update_tutar_aktif_iade_topunun_altina_inemez()
    {
        var expense = Create().Value;
        expense.AddRefund(60m, "TRY", OccurredOn, null, Today, Now);

        var result = expense.Update(50m, "TRY", OccurredOn, CategoryId, null, PaymentMethod.Cash, null, Today);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseAmountBelowRefunds", result.Error.Code);
    }

    [Fact]
    public void Update_harcama_tarihi_aktif_iadenin_sonrasina_alinamaz()
    {
        var expense = Create().Value;
        expense.AddRefund(10m, "TRY", OccurredOn, null, Today, Now);

        var result = expense.Update(
            100m,
            "TRY",
            OccurredOn.AddDays(1),
            CategoryId,
            null,
            PaymentMethod.Cash,
            null,
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseDateBeforeRefunds", result.Error.Code);
    }

    [Fact]
    public void Update_recordedAtUtc_degistirmez()
    {
        var expense = Create().Value;
        var recordedAt = expense.RecordedAtUtc;

        var result = expense.Update(200m, "TRY", OccurredOn, CategoryId, "yeni", PaymentMethod.Cash, null, Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(recordedAt, expense.RecordedAtUtc);
        Assert.Equal(200m, expense.Amount);
    }

    [Fact]
    public void AddRefund_kismi_ve_tam_iade_durumlarini_turetir()
    {
        var expense = Create(amount: 100m).Value;

        var partial = expense.AddRefund(40m, "TRY", OccurredOn, "kısmi", Today, Now);

        Assert.True(partial.IsSuccess);
        Assert.Equal(40m, expense.RefundedAmount);
        Assert.Equal(60m, expense.NetAmount);
        Assert.Equal(ExpenseStatus.PartiallyRefunded, expense.Status);

        var full = expense.AddRefund(60m, "TRY", OccurredOn, null, Today, Now);

        Assert.True(full.IsSuccess);
        Assert.Equal(100m, expense.RefundedAmount);
        Assert.Equal(0m, expense.NetAmount);
        Assert.Equal(ExpenseStatus.Refunded, expense.Status);
    }

    [Fact]
    public void AddRefund_tutar_harcamayi_asamaz()
    {
        var expense = Create(amount: 100m).Value;

        var result = expense.AddRefund(101m, "TRY", OccurredOn, null, Today, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("RefundExceedsExpense", result.Error.Code);
        Assert.Empty(expense.Refunds);
    }

    [Fact]
    public void AddRefund_tarih_harcamadan_once_olamaz()
    {
        var expense = Create().Value;

        var result = expense.AddRefund(10m, "TRY", OccurredOn.AddDays(-1), null, Today, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("RefundDateBeforeExpense", result.Error.Code);
    }

    [Fact]
    public void AddRefund_gelecek_tarih_reddedilir()
    {
        var expense = Create().Value;

        var result = expense.AddRefund(10m, "TRY", Today.AddDays(1), null, Today, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("RefundDateInFuture", result.Error.Code);
    }

    [Fact]
    public void AddRefund_gerekce_dogrulanmaz_ama_iptal_harcamaya_eklenemez()
    {
        var expense = Create().Value;
        expense.Cancel("Yanlış kayıt", Now);

        var result = expense.AddRefund(10m, "TRY", OccurredOn, null, Today, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCancelled", result.Error.Code);
    }

    [Fact]
    public void Cancel_aktif_iade_varsa_reddedilir()
    {
        var expense = Create().Value;
        expense.AddRefund(10m, "TRY", OccurredOn, null, Today, Now);

        var result = expense.Cancel("Yanlış kayıt", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseHasActiveRefunds", result.Error.Code);
        Assert.Equal(ExpenseStatus.PartiallyRefunded, expense.Status);
    }

    [Fact]
    public void Cancel_gerekce_zorunlu_ve_idempotent()
    {
        var expense = Create().Value;

        var missingReason = expense.Cancel(" ", Now);
        Assert.True(missingReason.IsFailure);
        Assert.Equal("CancellationReasonRequired", missingReason.Error.Code);

        var cancelled = expense.Cancel("Yanlış kayıt", Now);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(ExpenseStatus.Cancelled, expense.Status);
        Assert.Equal("Yanlış kayıt", expense.CancellationReason);

        var again = expense.Cancel(null, Now.AddMinutes(5));
        Assert.True(again.IsSuccess);
        Assert.Equal(Now, expense.CancelledAtUtc);
    }

    [Fact]
    public void CancelRefund_net_tutari_yukseltir_ve_idempotent()
    {
        var expense = Create(amount: 100m).Value;
        var refund = expense.AddRefund(40m, "TRY", OccurredOn, null, Today, Now).Value;

        var cancelled = expense.CancelRefund(refund.Id, "Müşteri vazgeçti", Now);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal(0m, expense.RefundedAmount);
        Assert.Equal(100m, expense.NetAmount);
        Assert.Equal(ExpenseStatus.Active, expense.Status);
        Assert.Equal(RefundStatus.Cancelled, refund.Status);

        var again = expense.CancelRefund(refund.Id, null, Now.AddMinutes(1));
        Assert.True(again.IsSuccess);
        Assert.Equal(Now, refund.CancelledAtUtc);
    }

    [Fact]
    public void CancelRefund_bilinmeyen_iade_icin_notfound_doner()
    {
        var expense = Create().Value;

        var result = expense.CancelRefund(Guid.NewGuid(), "Sebep", Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void RefundedAmount_iptal_edilen_iadeyi_saymaz()
    {
        var expense = Create(amount: 100m).Value;
        var first = expense.AddRefund(30m, "TRY", OccurredOn, null, Today, Now).Value;
        expense.AddRefund(20m, "TRY", OccurredOn, null, Today, Now);

        expense.CancelRefund(first.Id, "iptal", Now);

        Assert.Equal(20m, expense.RefundedAmount);
        Assert.Equal(80m, expense.NetAmount);
    }

    private static Result<Expense> Create(
        decimal amount = 100m,
        string? currency = "TRY",
        DateOnly? occurredOn = null,
        string? description = "test",
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        Guid? cardId = null) =>
        Expense.Create(
            UserId,
            amount,
            currency,
            occurredOn ?? OccurredOn,
            CategoryId,
            description,
            paymentMethod,
            cardId,
            Today,
            Now);
}
