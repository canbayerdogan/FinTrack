using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Domain;

/// <summary>
/// Kesit gün sonunun UTC sınırına çevrilmesi (docs/05 §5.3, senaryo 18).
/// </summary>
public sealed class UserTimeCutoffTests
{
    [Fact]
    public void EndOfDayUtc_utc_arti_14_sinirini_dogru_ana_cevirir()
    {
        var boundary = UserTime.EndOfDayUtc(new DateOnly(2026, 11, 14), "Pacific/Kiritimati");

        // Yerel gün sonu (23:59:59.9999999) UTC+14'te 2026-11-14T09:59:59.9999999Z olur.
        Assert.True(boundary > new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc));
        Assert.True(boundary < new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void EndOfDayUtc_utc_eksi_11_sinirini_dogru_ana_cevirir()
    {
        var boundary = UserTime.EndOfDayUtc(new DateOnly(2026, 11, 14), "Pacific/Pago_Pago");

        // Yerel gün sonu UTC-11'de ertesi gün 10:59:59.9999999Z olur.
        Assert.True(boundary > new DateTime(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc));
        Assert.True(boundary < new DateTime(2026, 11, 15, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void EndOfDayUtc_istanbul_icin_uc_saat_geri_kaydirir()
    {
        var boundary = UserTime.EndOfDayUtc(new DateOnly(2026, 11, 14), "Europe/Istanbul");

        Assert.True(boundary > new DateTime(2026, 11, 14, 20, 0, 0, DateTimeKind.Utc));
        Assert.True(boundary < new DateTime(2026, 11, 14, 21, 0, 0, DateTimeKind.Utc));
    }
}
