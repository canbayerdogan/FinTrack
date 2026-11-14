using FinTrack.Modules.CreditCards.Domain;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Domain;

public sealed class CreditCardDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_gecerli_karti_normalize_eder()
    {
        var result = CreditCard.Create(
            UserId,
            "  Örnek Bank  ",
            "  Platinum ",
            "  ornek-bank ",
            50_000m,
            25,
            5,
            Now);

        Assert.True(result.IsSuccess);
        var card = result.Value;
        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal("Örnek Bank", card.BankName);
        Assert.Equal("Platinum", card.DisplayName);
        Assert.Equal("ornek-bank", card.LogoKey);
        Assert.Equal(50_000m, card.CreditLimit);
        Assert.Equal(25, card.StatementDay);
        Assert.Equal(5, card.PaymentDueDay);
        Assert.Equal(CardStatus.Active, card.Status);
        Assert.False(card.IsArchived);
        Assert.Equal(Now, card.CreatedAtUtc);
    }

    [Fact]
    public void Create_zorunlu_alanlari_reddeder()
    {
        var noBank = CreditCard.Create(UserId, " ", "Platinum", null, null, null, null, Now);
        Assert.Equal("CardBankNameRequired", noBank.Error.Code);

        var noName = CreditCard.Create(UserId, "Örnek Bank", null, null, null, null, null, Now);
        Assert.Equal("CardDisplayNameRequired", noName.Error.Code);

        var noUser = CreditCard.Create(Guid.Empty, "Örnek Bank", "Platinum", null, null, null, null, Now);
        Assert.Equal("UserRequired", noUser.Error.Code);
    }

    [Fact]
    public void Create_uzunluk_sinirlarini_uygular()
    {
        var bank = CreditCard.Create(
            UserId, new string('b', CreditCard.BankNameMaxLength + 1), "Kart", null, null, null, null, Now);
        Assert.Equal("CardBankNameTooLong", bank.Error.Code);

        var name = CreditCard.Create(
            UserId, "Banka", new string('n', CreditCard.DisplayNameMaxLength + 1), null, null, null, null, Now);
        Assert.Equal("CardDisplayNameTooLong", name.Error.Code);

        var logo = CreditCard.Create(
            UserId, "Banka", "Kart", new string('l', CreditCard.LogoKeyMaxLength + 1), null, null, null, Now);
        Assert.Equal("CardLogoKeyTooLong", logo.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_sifir_veya_negatif_limiti_reddeder(int limit)
    {
        var result = CreditCard.Create(UserId, "Banka", "Kart", null, limit, null, null, Now);
        Assert.Equal("CardCreditLimitInvalid", result.Error.Code);
    }

    [Fact]
    public void Create_dort_ondaliktan_fazla_limiti_reddeder()
    {
        var result = CreditCard.Create(UserId, "Banka", "Kart", null, 10.12345m, null, null, Now);
        Assert.Equal("CardCreditLimitScale", result.Error.Code);
    }

    [Fact]
    public void Create_asiri_buyuk_limiti_reddeder()
    {
        var result = CreditCard.Create(
            UserId, "Banka", "Kart", null, CreditCard.MaxCreditLimit + 1m, null, null, Now);
        Assert.Equal("CardCreditLimitTooLarge", result.Error.Code);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(32, null)]
    [InlineData(null, 0)]
    [InlineData(null, 32)]
    public void Create_gun_araligini_uygular(int? statementDay, int? paymentDueDay)
    {
        var result = CreditCard.Create(
            UserId, "Banka", "Kart", null, null, statementDay, paymentDueDay, Now);

        if (statementDay is not null)
        {
            Assert.Equal("CardStatementDayRange", result.Error.Code);
        }
        else
        {
            Assert.Equal("CardPaymentDueDayRange", result.Error.Code);
        }
    }

    [Fact]
    public void Create_opsiyonel_alanlar_null_olabilir()
    {
        var result = CreditCard.Create(UserId, "Banka", "Kart", null, null, null, null, Now);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.CreditLimit);
        Assert.Null(result.Value.StatementDay);
        Assert.Null(result.Value.PaymentDueDay);
        Assert.Null(result.Value.LogoKey);
    }

    [Fact]
    public void Archive_idempotenttir_ve_durumu_degistirir()
    {
        var card = CreditCard.Create(UserId, "Banka", "Kart", null, null, 25, 5, Now).Value;
        var archivedAt = Now.AddDays(1);

        Assert.True(card.Archive(archivedAt).IsSuccess);
        Assert.Equal(CardStatus.Archived, card.Status);
        Assert.Equal(archivedAt, card.ArchivedAtUtc);

        Assert.True(card.Archive(archivedAt.AddDays(1)).IsSuccess);
        Assert.Equal(archivedAt, card.ArchivedAtUtc);
    }

    [Fact]
    public void Update_alanlari_degistirir_ve_arsivli_kartta_da_calisir()
    {
        var card = CreditCard.Create(UserId, "Banka", "Kart", "logo", 1000m, 10, 20, Now).Value;
        card.Archive(Now.AddDays(1));

        var result = card.Update("Yeni Banka", "Yeni Kart", null, null, 15, 25);

        Assert.True(result.IsSuccess);
        Assert.Equal("Yeni Banka", card.BankName);
        Assert.Equal("Yeni Kart", card.DisplayName);
        Assert.Null(card.LogoKey);
        Assert.Null(card.CreditLimit);
        Assert.Equal(15, card.StatementDay);
        Assert.Equal(25, card.PaymentDueDay);
        Assert.Equal(CardStatus.Archived, card.Status);
    }

    [Fact]
    public void Update_gecersiz_veriyi_reddeder_ve_mevcut_degerleri_korur()
    {
        var card = CreditCard.Create(UserId, "Banka", "Kart", null, 1000m, 10, 20, Now).Value;

        var result = card.Update(" ", "Yeni Kart", null, null, 15, 25);

        Assert.Equal("CardBankNameRequired", result.Error.Code);
        Assert.Equal("Banka", card.BankName);
        Assert.Equal("Kart", card.DisplayName);
    }
}
