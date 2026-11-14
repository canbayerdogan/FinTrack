using FinTrack.Modules.Expenses.Domain;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Domain;

public sealed class CategoryDomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_adi_trim_eder_ve_normalize_eder()
    {
        var result = Category.Create(UserId, "  Market  ", " cart ", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Market", result.Value.Name);
        Assert.Equal("MARKET", result.Value.NormalizedName);
        Assert.Equal("cart", result.Value.IconKey);
        Assert.False(result.Value.IsArchived);
        Assert.Equal(Now, result.Value.CreatedAtUtc);
    }

    [Fact]
    public void Create_bos_ad_reddedilir()
    {
        var result = Category.Create(UserId, "   ", null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNameRequired", result.Error.Code);
    }

    [Fact]
    public void Create_100_karakterden_uzun_ad_reddedilir()
    {
        var result = Category.Create(UserId, new string('a', Category.NameMaxLength + 1), null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNameTooLong", result.Error.Code);
    }

    [Fact]
    public void Create_50_karakterden_uzun_ikon_reddedilir()
    {
        var result = Category.Create(UserId, "Market", new string('i', Category.IconKeyMaxLength + 1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("CategoryIconTooLong", result.Error.Code);
    }

    [Fact]
    public void Rename_normalize_edilmis_adi_gunceller()
    {
        var category = Category.Create(UserId, "Market", null, Now).Value;

        var result = category.Rename("  Gıda ", null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Gıda", category.Name);
        Assert.Equal("GIDA", category.NormalizedName);
        Assert.Null(category.IconKey);
    }

    [Fact]
    public void Archive_idempotent_ve_ilk_zamani_korur()
    {
        var category = Category.Create(UserId, "Market", null, Now).Value;

        category.Archive(Now);
        category.Archive(Now.AddHours(1));

        Assert.True(category.IsArchived);
        Assert.Equal(Now, category.ArchivedAtUtc);
    }
}
