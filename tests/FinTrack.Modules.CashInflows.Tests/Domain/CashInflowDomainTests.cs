using FinTrack.Modules.CashInflows.Domain;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.CashInflows.Tests.Domain;

public sealed class CashInflowDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateOnly OccurredOn = Today.AddDays(-2);
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(CashInflowSource.Salary, true)]
    [InlineData(CashInflowSource.BusinessIncome, true)]
    [InlineData(CashInflowSource.BankLoan, false)]
    [InlineData(CashInflowSource.CashAdvance, false)]
    [InlineData(CashInflowSource.PersonalBorrowing, false)]
    [InlineData(CashInflowSource.AssetSale, false)]
    [InlineData(CashInflowSource.Unknown, null)]
    public void Kaynak_gelir_niteligini_turetir(CashInflowSource source, bool? expected)
    {
        Assert.Equal(expected, CashInflow.EarnedIncomeFor(source));
    }

    [Fact]
    public void Create_gecerli_gelir_aktif_ve_alanlar_dogru()
    {
        var result = Create(description: "  Kasım maaşı  ");

        Assert.True(result.IsSuccess);
        var inflow = result.Value;
        Assert.Equal(UserId, inflow.UserId);
        Assert.Equal(80_000m, inflow.Amount);
        Assert.Equal("TRY", inflow.Currency);
        Assert.Equal(OccurredOn, inflow.OccurredOn);
        Assert.Equal(CashInflowSource.Salary, inflow.Source);
        Assert.True(inflow.IsEarnedIncome);
        Assert.Equal("Kasım maaşı", inflow.Description);
        Assert.Equal(CashInflowStatus.Active, inflow.Status);
        Assert.Null(inflow.DebtLinkId);
        Assert.Null(inflow.DebtType);
        Assert.Equal(Now, inflow.RecordedAtUtc);
    }

    [Fact]
    public void AssetSale_gelir_degildir_ve_borc_baglantisi_yoktur()
    {
        var inflow = Create(source: CashInflowSource.AssetSale).Value;

        Assert.False(inflow.IsEarnedIncome);
        Assert.Null(inflow.DebtType);
    }

    [Fact]
    public void Unknown_gelir_niteligi_belirsizdir()
    {
        var inflow = Create(source: CashInflowSource.Unknown).Value;

        Assert.Null(inflow.IsEarnedIncome);
        Assert.Equal(CashInflowStatus.Active, inflow.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_sifir_veya_negatif_tutar_reddedilir(decimal amount)
    {
        var result = Create(amount: amount);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowAmountInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_dort_ondaliktan_fazla_basamak_reddedilir()
    {
        var result = Create(amount: 10.12345m);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowAmountScale", result.Error.Code);
    }

    [Fact]
    public void Create_ust_sinir_asilmasi_reddedilir()
    {
        var result = Create(amount: CashInflow.MaxAmount + 1m);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowAmountTooLarge", result.Error.Code);
    }

    [Fact]
    public void Create_desteklenmeyen_para_birimi_reddedilir()
    {
        var result = Create(currency: "USD");

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowCurrencyUnsupported", result.Error.Code);
    }

    [Fact]
    public void Create_tarih_zorunludur()
    {
        var result = Create(occurredOn: DateOnly.MinValue);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowDateRequired", result.Error.Code);
    }

    [Fact]
    public void Create_gelecek_tarih_reddedilir()
    {
        var result = Create(occurredOn: Today.AddDays(1));

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowDateInFuture", result.Error.Code);
    }

    [Fact]
    public void Create_gecersiz_kaynak_reddedilir()
    {
        var result = Create(source: (CashInflowSource)99);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowSourceInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_uzun_aciklama_reddedilir()
    {
        var result = Create(description: new string('a', CashInflow.DescriptionMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowDescriptionTooLong", result.Error.Code);
    }

    [Fact]
    public void Create_kullanici_zorunludur()
    {
        var result = Create(userId: Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("UserRequired", result.Error.Code);
    }

    [Theory]
    [InlineData(CashInflowSource.BankLoan)]
    [InlineData(CashInflowSource.CashAdvance)]
    [InlineData(CashInflowSource.PersonalBorrowing)]
    public void Create_borc_doguran_kaynagi_reddeder(CashInflowSource source)
    {
        var result = Create(source: source);

        Assert.True(result.IsFailure);
        Assert.Equal("DebtSourceRequiresOwnerEndpoint", result.Error.Code);
    }

    [Theory]
    [InlineData(CashInflowSource.BankLoan, DebtLinkType.Loan)]
    [InlineData(CashInflowSource.CashAdvance, DebtLinkType.CashAdvance)]
    [InlineData(CashInflowSource.PersonalBorrowing, DebtLinkType.PersonalDebt)]
    public void CreateFromDebtOwner_borc_baglantisini_kurar(
        CashInflowSource source,
        DebtLinkType expectedType)
    {
        var debtLinkId = Guid.NewGuid();

        var result = CashInflow.CreateFromDebtOwner(
            UserId,
            25_000m,
            "try",
            OccurredOn,
            source,
            debtLinkId,
            "Borç girişi",
            Today,
            Now);

        Assert.True(result.IsSuccess);
        var inflow = result.Value;
        Assert.Equal(debtLinkId, inflow.DebtLinkId);
        Assert.Equal(expectedType, inflow.DebtType);
        Assert.False(inflow.IsEarnedIncome);
    }

    [Fact]
    public void CreateFromDebtOwner_borc_disi_kaynagi_reddeder()
    {
        var result = CashInflow.CreateFromDebtOwner(
            UserId,
            100m,
            "TRY",
            OccurredOn,
            CashInflowSource.Salary,
            Guid.NewGuid(),
            null,
            Today,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowSourceInvalid", result.Error.Code);
    }

    [Fact]
    public void Update_alanlari_degistirir_ve_gelir_niteligini_yeniden_turetir()
    {
        var inflow = Create().Value;

        var result = inflow.Update(
            95_000m,
            "try",
            Today.AddDays(-1),
            CashInflowSource.BusinessIncome,
            "  Yeni açıklama ",
            Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(95_000m, inflow.Amount);
        Assert.Equal(Today.AddDays(-1), inflow.OccurredOn);
        Assert.Equal(CashInflowSource.BusinessIncome, inflow.Source);
        Assert.True(inflow.IsEarnedIncome);
        Assert.Equal("Yeni açıklama", inflow.Description);
        Assert.Equal(Now, inflow.RecordedAtUtc);
    }

    [Fact]
    public void Update_borc_doguran_kaynaga_cevrilemez()
    {
        var inflow = Create().Value;

        var result = inflow.Update(
            100m,
            "TRY",
            OccurredOn,
            CashInflowSource.BankLoan,
            null,
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("DebtSourceRequiresOwnerEndpoint", result.Error.Code);
        Assert.Equal(CashInflowSource.Salary, inflow.Source);
    }

    [Fact]
    public void Update_iptal_edilmis_kaydi_reddeder()
    {
        var inflow = Create().Value;
        Assert.True(inflow.Cancel("Yanlış giriş", Now).IsSuccess);

        var result = inflow.Update(
            100m,
            "TRY",
            OccurredOn,
            CashInflowSource.Salary,
            null,
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("StateConflict", result.Error.Code);
    }

    [Fact]
    public void Update_borc_bagli_kaydi_reddeder()
    {
        var inflow = CreateDebtLinked().Value;

        var result = inflow.Update(
            100m,
            "TRY",
            OccurredOn,
            CashInflowSource.CashAdvance,
            null,
            Today);

        Assert.True(result.IsFailure);
        Assert.Equal("StateConflict", result.Error.Code);
    }

    [Fact]
    public void Cancel_gerekcesiz_reddedilir_gerekceli_idempotenttir()
    {
        var inflow = Create().Value;

        var missing = inflow.Cancel(null, Now.AddMinutes(1));
        Assert.True(missing.IsFailure);
        Assert.Equal("CancellationReasonRequired", missing.Error.Code);
        Assert.Equal(CashInflowStatus.Active, inflow.Status);

        var blank = inflow.Cancel("   ", Now.AddMinutes(1));
        Assert.True(blank.IsFailure);
        Assert.Equal("CancellationReasonRequired", blank.Error.Code);

        var cancelled = inflow.Cancel("  Yanlış giriş  ", Now.AddMinutes(2));
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(CashInflowStatus.Cancelled, inflow.Status);
        Assert.Equal(Now.AddMinutes(2), inflow.CancelledAtUtc);
        Assert.Equal("Yanlış giriş", inflow.CancellationReason);

        // İkinci iptal hata üretmez ve zamanı değiştirmez.
        var again = inflow.Cancel("Başka", Now.AddHours(1));
        Assert.True(again.IsSuccess);
        Assert.Equal(Now.AddMinutes(2), inflow.CancelledAtUtc);
        Assert.Equal("Yanlış giriş", inflow.CancellationReason);
    }

    [Fact]
    public void Cancel_borc_bagli_kaydi_reddeder()
    {
        var inflow = CreateDebtLinked().Value;

        var result = inflow.Cancel("İptal", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("StateConflict", result.Error.Code);
        Assert.Equal(CashInflowStatus.Active, inflow.Status);
    }

    [Fact]
    public void Cancel_uzun_gerekce_reddedilir()
    {
        var inflow = Create().Value;

        var result = inflow.Cancel(new string('a', CashInflow.CancellationReasonMaxLength + 1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("CancellationReasonTooLong", result.Error.Code);
    }

    [Fact]
    public void LinkToDebt_borc_disi_kaynakta_reddedilir()
    {
        var inflow = Create(source: CashInflowSource.AssetSale).Value;

        var result = inflow.LinkToDebt(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowSourceInvalid", result.Error.Code);
        Assert.Null(inflow.DebtLinkId);
    }

    [Fact]
    public void LinkToDebt_zaten_bagli_kaydi_reddeder()
    {
        var inflow = CreateDebtLinked().Value;

        var result = inflow.LinkToDebt(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("StateConflict", result.Error.Code);
    }

    private static Result<CashInflow> Create(
        Guid? userId = null,
        decimal amount = 80_000m,
        string? currency = "TRY",
        DateOnly? occurredOn = null,
        CashInflowSource source = CashInflowSource.Salary,
        string? description = "Kasım maaşı") =>
        CashInflow.Create(
            userId ?? UserId,
            amount,
            currency,
            occurredOn ?? OccurredOn,
            source,
            description,
            Today,
            Now);

    private static Result<CashInflow> CreateDebtLinked() =>
        CashInflow.CreateFromDebtOwner(
            UserId,
            15_000m,
            "TRY",
            OccurredOn,
            CashInflowSource.CashAdvance,
            Guid.NewGuid(),
            "Nakit avans",
            Today,
            Now);
}
