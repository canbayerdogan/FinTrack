using FinTrack.Modules.Expenses.Tests.Support;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Domain;

public sealed class UserTimeTests
{
    [Fact]
    public void Today_istanbul_saat_diliminde_takvim_gununu_kaydirir()
    {
        var clock = new TestClock(new DateTime(2026, 11, 13, 22, 30, 0, DateTimeKind.Utc));

        var today = UserTime.Today(clock, "Europe/Istanbul");

        Assert.Equal(new DateOnly(2026, 11, 14), today);
    }

    [Fact]
    public void Today_gecersiz_saat_diliminde_varsayilana_duser()
    {
        var clock = new TestClock(new DateTime(2026, 11, 13, 22, 30, 0, DateTimeKind.Utc));

        var today = UserTime.Today(clock, "Mars/Olympus");

        Assert.Equal(new DateOnly(2026, 11, 14), today);
    }

    [Fact]
    public void ResolveTimeZone_bos_degerde_istanbul_doner()
    {
        var zone = UserTime.ResolveTimeZone(null);

        Assert.Equal("Europe/Istanbul", zone.Id);
    }
}
