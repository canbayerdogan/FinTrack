using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.Modules.Expenses.Tests.Support;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Application;

public sealed class CategoryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Kategori_olusturulur_ve_listelenir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);

        var created = await handlers.CreateCategory().HandleAsync(new CreateCategoryCommand("Market", "cart"), default);

        Assert.True(created.IsSuccess);
        Assert.Equal("Market", created.Value.Name);
        Assert.Equal(8, Convert.FromBase64String(created.Value.RowVersion).Length);

        var list = await handlers.GetCategories().HandleAsync(new GetCategoriesQuery(false), default);

        Assert.True(list.IsSuccess);
        Assert.Single(list.Value);
        Assert.Equal(created.Value.Id, list.Value[0].Id);
    }

    [Fact]
    public async Task Kategori_adi_buyuk_kucuk_harf_duyarsiz_benzersizdir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);

        var first = await handlers.CreateCategory().HandleAsync(new CreateCategoryCommand("Market", null), default);
        Assert.True(first.IsSuccess);

        var duplicate = await handlers.CreateCategory().HandleAsync(new CreateCategoryCommand("  market ", null), default);

        Assert.True(duplicate.IsFailure);
        Assert.Equal("StateConflict", duplicate.Error.Code);
    }

    [Fact]
    public async Task Kategori_guncellemesi_eski_surumle_409_doner()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);

        var created = await handlers.CreateCategory().HandleAsync(new CreateCategoryCommand("Market", null), default);
        Assert.True(created.IsSuccess);

        var update = await handlers.UpdateCategory().HandleAsync(
            new UpdateCategoryCommand(created.Value.Id, "Gıda", "cart", created.Value.RowVersion),
            default);
        Assert.True(update.IsSuccess);
        Assert.NotEqual(created.Value.RowVersion, update.Value.RowVersion);

        var stale = await handlers.UpdateCategory().HandleAsync(
            new UpdateCategoryCommand(created.Value.Id, "Başka", null, created.Value.RowVersion),
            default);

        Assert.True(stale.IsFailure);
        Assert.Equal("ConcurrencyConflict", stale.Error.Code);
    }

    [Fact]
    public async Task Arsivleme_idempotenttir_ve_varsayilan_listeden_cikarir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);

        var created = await handlers.CreateCategory().HandleAsync(new CreateCategoryCommand("Market", null), default);
        Assert.True(created.IsSuccess);

        var archived = await handlers.ArchiveCategory()
            .HandleAsync(new ArchiveCategoryCommand(created.Value.Id, created.Value.RowVersion), default);
        Assert.True(archived.IsSuccess);

        var stored = await handlers.Categories.FindAsync(userId, created.Value.Id, default);
        Assert.NotNull(stored);
        var secondArchive = await handlers.ArchiveCategory()
            .HandleAsync(new ArchiveCategoryCommand(stored!.Id, Convert.ToBase64String(stored.RowVersion)), default);
        Assert.True(secondArchive.IsSuccess);

        var defaultList = await handlers.GetCategories().HandleAsync(new GetCategoriesQuery(false), default);
        Assert.True(defaultList.IsSuccess);
        Assert.Empty(defaultList.Value);

        var all = await handlers.GetCategories().HandleAsync(new GetCategoriesQuery(true), default);
        Assert.True(all.IsSuccess);
        Assert.Single(all.Value);
    }
}
