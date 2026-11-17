using FinTrack.Modules.Debts.Domain;
using Xunit;

namespace FinTrack.Modules.Debts.Tests.Domain;

public sealed class LoanDomainTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);

    [Fact]
    public void Olusturma_tum_vade_icin_taksit_plani_uretir()
    {
        var result = Loan.Create(
            Guid.NewGuid(), 30_000m, 3_000m, 12,
            new DateOnly(2026, 12, 1), null, null, null, Today, NowUtc);

        Assert.True(result.IsSuccess);
        var loan = result.Value;

        Assert.Equal(12, loan.Installments.Count);
        Assert.All(loan.Installments, installment =>
        {
            Assert.Equal(3_000m, installment.Amount);
            Assert.Equal(InstallmentStatus.Planned, installment.Status);
        });
        Assert.Equal(new DateOnly(2026, 12, 1), loan.Installments.First().DueDate);
        Assert.Equal(new DateOnly(2027, 11, 1), loan.Installments.Last().DueDate);
        Assert.Equal(36_000m, loan.TotalRepayment);
        Assert.Equal(36_000m, loan.RemainingAmount);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public void Vade_gunu_yoksa_ay_sonuna_sabitlenir()
    {
        var result = Loan.Create(
            Guid.NewGuid(), 3_000m, 1_000m, 3,
            new DateOnly(2026, 12, 31), null, null, null, Today, NowUtc);

        Assert.True(result.IsSuccess);
        var dueDates = result.Value.Installments.Select(installment => installment.DueDate).ToArray();

        Assert.Equal(new DateOnly(2026, 12, 31), dueDates[0]);
        Assert.Equal(new DateOnly(2027, 1, 31), dueDates[1]);
        Assert.Equal(new DateOnly(2027, 2, 28), dueDates[2]);
    }

    [Fact]
    public void Toplam_geri_odeme_anagaradan_kucukse_red_edilir()
    {
        var result = Loan.Create(
            Guid.NewGuid(), 30_000m, 2_000m, 12,
            new DateOnly(2026, 12, 1), null, null, null, Today, NowUtc);

        Assert.True(result.IsFailure);
        Assert.Equal("LoanTotalRepaymentBelowPrincipal", result.Error.Code);
    }

    [Fact]
    public void Taksitler_sirayla_odenir()
    {
        var loan = CreateLoan(3);

        var outOfOrder = loan.PayInstallment(2, Today, Today, NowUtc);

        Assert.True(outOfOrder.IsFailure);
        Assert.Equal("StateConflict", outOfOrder.Error.Code);
        Assert.True(loan.PayInstallment(1, Today, Today, NowUtc).IsSuccess);
        Assert.True(loan.PayInstallment(2, Today, Today, NowUtc).IsSuccess);
    }

    [Fact]
    public void Odeme_kalan_borcu_azaltir_ve_iptal_geri_yukseltir()
    {
        var loan = CreateLoan(3);
        var payment = loan.PayInstallment(1, Today, Today, NowUtc);

        Assert.True(payment.IsSuccess);
        Assert.Equal(2_000m, loan.RemainingAmount);

        var cancel = loan.CancelInstallmentPayment(1, payment.Value.Id, "Yanlış ödeme", NowUtc);

        Assert.True(cancel.IsSuccess);
        Assert.Equal(InstallmentStatus.Planned, loan.Installments.First().Status);
        Assert.Equal(3_000m, loan.RemainingAmount);
    }

    [Fact]
    public void Odeme_iptalinde_sirada_son_taksit_kisiti_yoktur()
    {
        var loan = CreateLoan(3);
        var first = loan.PayInstallment(1, Today, Today, NowUtc);
        var second = loan.PayInstallment(2, Today, Today, NowUtc);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var cancel = loan.CancelInstallmentPayment(1, first.Value.Id, "İptal", NowUtc);

        Assert.True(cancel.IsSuccess);
        Assert.Equal(InstallmentStatus.Planned, loan.Installments.First().Status);
        Assert.Equal(InstallmentStatus.Paid, loan.Installments.Skip(1).First().Status);
    }

    [Fact]
    public void Odeme_bittiğinde_kredi_kapanir()
    {
        var loan = CreateLoan(1);

        Assert.True(loan.PayInstallment(1, Today, Today, NowUtc).IsSuccess);
        Assert.Equal(LoanStatus.Closed, loan.Status);
        Assert.Equal(0m, loan.RemainingAmount);
    }

    [Fact]
    public void Odenmis_taksit_varken_kredi_iptal_edilemez()
    {
        var loan = CreateLoan(2);
        Assert.True(loan.PayInstallment(1, Today, Today, NowUtc).IsSuccess);

        var cancel = loan.Cancel("Vazgeçtim", NowUtc);

        Assert.True(cancel.IsFailure);
        Assert.Equal("StateConflict", cancel.Error.Code);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public void Gelecek_tarihli_odeme_red_edilir()
    {
        var loan = CreateLoan(1);

        var result = loan.PayInstallment(1, Today.AddDays(1), Today, NowUtc);

        Assert.True(result.IsFailure);
        Assert.Equal("PaymentDateInFuture", result.Error.Code);
    }

    private static Loan CreateLoan(int installmentCount)
    {
        var result = Loan.Create(
            Guid.NewGuid(),
            1_000m * installmentCount,
            1_000m,
            installmentCount,
            new DateOnly(2026, 12, 1),
            null,
            null,
            null,
            Today,
            NowUtc);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
