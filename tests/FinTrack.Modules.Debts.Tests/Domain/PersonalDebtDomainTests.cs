using FinTrack.Modules.Debts.Domain;
using Xunit;

namespace FinTrack.Modules.Debts.Tests.Domain;

public sealed class PersonalDebtDomainTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);

    [Fact]
    public void Kalan_tutar_aktif_geriOdemeler_uzerinden_turetilir()
    {
        var debt = CreateDebt(5_000m);

        var first = debt.AddRepayment(1_000m, Today, "İlk", Today, NowUtc);
        var second = debt.AddRepayment(500m, Today, null, Today, NowUtc);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(3_500m, debt.RemainingAmount);
        Assert.Equal(PersonalDebtStatus.Active, debt.Status);
    }

    [Fact]
    public void Tam_geriOdeme_borcu_kapatir()
    {
        var debt = CreateDebt(1_000m);

        Assert.True(debt.AddRepayment(1_000m, Today, null, Today, NowUtc).IsSuccess);
        Assert.Equal(0m, debt.RemainingAmount);
        Assert.Equal(PersonalDebtStatus.Closed, debt.Status);
    }

    [Fact]
    public void GeriOdeme_iptali_kalan_tutari_geri_yukseltir()
    {
        var debt = CreateDebt(1_000m);
        var repayment = debt.AddRepayment(1_000m, Today, null, Today, NowUtc);

        Assert.True(repayment.IsSuccess);
        Assert.Equal(PersonalDebtStatus.Closed, debt.Status);

        Assert.True(debt.CancelRepayment(repayment.Value.Id, "Yanlış", NowUtc).IsSuccess);
        Assert.Equal(1_000m, debt.RemainingAmount);
        Assert.Equal(PersonalDebtStatus.Active, debt.Status);
    }

    [Fact]
    public void GeriOdeme_kalan_tutari_asamaz()
    {
        var debt = CreateDebt(1_000m);

        var result = debt.AddRepayment(1_000.01m, Today, null, Today, NowUtc);

        Assert.True(result.IsFailure);
        Assert.Equal("RepaymentExceedsRemaining", result.Error.Code);
    }

    [Fact]
    public void Aktif_geriOdeme_varken_borc_iptal_edilemez()
    {
        var debt = CreateDebt(1_000m);
        Assert.True(debt.AddRepayment(400m, Today, null, Today, NowUtc).IsSuccess);

        var cancel = debt.Cancel("Vazgeçtim", NowUtc);

        Assert.True(cancel.IsFailure);
        Assert.Equal("StateConflict", cancel.Error.Code);
    }

    [Fact]
    public void GeriOdeme_sonrasi_borc_iptal_edilebilir()
    {
        var debt = CreateDebt(1_000m);
        var repayment = debt.AddRepayment(400m, Today, null, Today, NowUtc);
        Assert.True(debt.CancelRepayment(repayment.Value.Id, "Yanlış", NowUtc).IsSuccess);

        Assert.True(debt.Cancel("Vazgeçtim", NowUtc).IsSuccess);
        Assert.Equal(PersonalDebtStatus.Cancelled, debt.Status);
    }

    [Fact]
    public void Ghalek_borc_bilgileri_ile_olusturulamaz()
    {
        var result = PersonalDebt.Create(
            Guid.NewGuid(), "Ahmet", 1_000m, Today.AddDays(1), null, null, null, Today, NowUtc);

        Assert.True(result.IsFailure);
        Assert.Equal("BorrowedOnInFuture", result.Error.Code);
    }

    private static PersonalDebt CreateDebt(decimal principal)
    {
        var result = PersonalDebt.Create(
            Guid.NewGuid(),
            "Ahmet",
            principal,
            Today.AddDays(-10),
            Today.AddDays(20),
            "Acil ihtiyaç",
            "TRY",
            Today,
            NowUtc);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
