using FinTrack.ModuleContracts;
using FinTrack.Modules.Expenses.Application.Categories;
using FinTrack.Modules.Expenses.Application.Expenses;
using FinTrack.Modules.Expenses.Domain;
using FinTrack.Modules.Expenses.Tests.Support;
using FinTrack.SharedKernel;
using Xunit;

namespace FinTrack.Modules.Expenses.Tests.Application;

public sealed class ExpenseHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateOnly OccurredOn = Today.AddDays(-1);

    [Fact]
    public async Task Harcama_olusturulur_ve_durumu_turetilir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);

        var result = await CreateExpenseAsync(handlers, category.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.Amount);
        Assert.Equal(100m, result.Value.NetAmount);
        Assert.Equal(0m, result.Value.RefundedAmount);
        Assert.Equal(ExpenseStatus.Active, result.Value.Status);
        Assert.Equal("Market", result.Value.CategoryName);
        Assert.Equal(Now, result.Value.RecordedAtUtc);
        Assert.Equal(8, Convert.FromBase64String(result.Value.RowVersion).Length);
    }

    [Fact]
    public async Task Arsivli_kategoriye_yeni_harcama_eklenemez()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);

        var archived = await handlers.ArchiveCategory()
            .HandleAsync(new ArchiveCategoryCommand(category.Id, category.RowVersion), default);
        Assert.True(archived.IsSuccess);

        var result = await CreateExpenseAsync(handlers, category.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCategoryArchived", result.Error.Code);
    }

    [Fact]
    public async Task Kart_modulu_yokken_kartli_harcama_kart_bulunamadi_doner()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);

        var result = await CreateExpenseAsync(
            handlers,
            category.Id,
            paymentMethod: PaymentMethod.CreditCard,
            cardId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseCardNotFound", result.Error.Code);
    }

    [Fact]
    public async Task Aktif_kartla_harcama_olusturulur_arsivli_kart_reddedilir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var activeCardId = Guid.NewGuid();

        using (var db = database.CreateContext(userId))
        {
            var handlers = new HandlerFactory(
                db,
                userId,
                Now,
                new FakeCardQuery(new CardReference(activeCardId, "Kart", IsActive: true)));
            var category = await CreateCategoryAsync(handlers);

            var created = await CreateExpenseAsync(
                handlers,
                category.Id,
                paymentMethod: PaymentMethod.CreditCard,
                cardId: activeCardId);
            Assert.True(created.IsSuccess);
            Assert.Equal(activeCardId, created.Value.CardId);
        }

        using (var db = database.CreateContext(userId))
        {
            var handlers = new HandlerFactory(
                db,
                userId,
                Now,
                new FakeCardQuery(new CardReference(activeCardId, "Kart", IsActive: false)));
            var category = await CreateCategoryAsync(handlers, "Diğer");

            var rejected = await CreateExpenseAsync(
                handlers,
                category.Id,
                paymentMethod: PaymentMethod.CreditCard,
                cardId: activeCardId);
            Assert.True(rejected.IsFailure);
            Assert.Equal("ExpenseCardArchived", rejected.Error.Code);
        }
    }

    [Fact]
    public async Task Iade_kismi_ve_tam_durumu_handler_uzerinden_gunceller()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);
        var expense = await CreateExpenseAsync(handlers, category.Id, amount: 100m);

        var partial = await handlers.AddRefund().HandleAsync(
            new AddRefundCommand(expense.Value.Id, 40m, "TRY", OccurredOn, "kısmi"),
            default);
        Assert.True(partial.IsSuccess, partial.IsFailure ? $"{partial.Error.Code}: {partial.Error.Message}" : null);
        Assert.Equal(RefundStatus.Active, partial.Value.Status);

        var afterPartial = await handlers.Expenses.FindAsync(userId, expense.Value.Id, default);
        Assert.NotNull(afterPartial);
        Assert.Equal(ExpenseStatus.PartiallyRefunded, afterPartial!.Status);
        Assert.Equal(60m, afterPartial.NetAmount);

        var full = await handlers.AddRefund().HandleAsync(
            new AddRefundCommand(expense.Value.Id, 60m, "TRY", OccurredOn, null),
            default);
        Assert.True(full.IsSuccess);

        var afterFull = await handlers.Expenses.FindAsync(userId, expense.Value.Id, default);
        Assert.NotNull(afterFull);
        Assert.Equal(ExpenseStatus.Refunded, afterFull!.Status);
        Assert.Equal(0m, afterFull.NetAmount);
    }

    [Fact]
    public async Task Iade_harcama_tutarini_asamaz()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);
        var expense = await CreateExpenseAsync(handlers, category.Id, amount: 100m);

        var result = await handlers.AddRefund().HandleAsync(
            new AddRefundCommand(expense.Value.Id, 101m, "TRY", OccurredOn, null),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("RefundExceedsExpense", result.Error.Code);
    }

    [Fact]
    public async Task Iptal_ve_iade_akisi_is_kurallarina_uyar()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);
        var expense = await CreateExpenseAsync(handlers, category.Id, amount: 100m);

        var refund = await handlers.AddRefund().HandleAsync(
            new AddRefundCommand(expense.Value.Id, 40m, "TRY", OccurredOn, null),
            default);
        Assert.True(refund.IsSuccess, refund.IsFailure ? $"{refund.Error.Code}: {refund.Error.Message}" : null);

        var blocked = await handlers.CancelExpense().HandleAsync(
            new CancelExpenseCommand(expense.Value.Id, "Yanlış kayıt", expense.Value.RowVersion),
            default);
        Assert.True(blocked.IsFailure);
        Assert.Equal("ExpenseHasActiveRefunds", blocked.Error.Code);

        var cancelRefund = await handlers.CancelRefund().HandleAsync(
            new CancelRefundCommand(expense.Value.Id, refund.Value.Id, "Müşteri vazgeçti", refund.Value.RowVersion),
            default);
        Assert.True(cancelRefund.IsSuccess);

        var cancelExpense = await handlers.CancelExpense().HandleAsync(
            new CancelExpenseCommand(expense.Value.Id, "Yanlış kayıt", expense.Value.RowVersion),
            default);
        Assert.True(cancelExpense.IsSuccess);

        var stored = await handlers.Expenses.FindAsync(userId, expense.Value.Id, default);
        Assert.NotNull(stored);
        Assert.Equal(ExpenseStatus.Cancelled, stored!.Status);
    }

    [Fact]
    public async Task Guncelleme_tutari_iade_topunun_altina_indiremez()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(db, userId, Now);
        var category = await CreateCategoryAsync(handlers);
        var expense = await CreateExpenseAsync(handlers, category.Id, amount: 100m);

        await handlers.AddRefund().HandleAsync(
            new AddRefundCommand(expense.Value.Id, 60m, "TRY", OccurredOn, null),
            default);

        var result = await handlers.UpdateExpense().HandleAsync(
            new UpdateExpenseCommand(
                expense.Value.Id,
                50m,
                "TRY",
                OccurredOn,
                category.Id,
                null,
                PaymentMethod.Cash,
                null,
                expense.Value.RowVersion),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("ExpenseAmountBelowRefunds", result.Error.Code);
    }

    [Fact]
    public async Task Guncelleme_eski_surumle_409_doner_ve_kart_temizlenir()
    {
        await using var database = await ExpenseTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        using var db = database.CreateContext(userId);
        var handlers = new HandlerFactory(
            db,
            userId,
            Now,
            new FakeCardQuery(new CardReference(cardId, "Kart", IsActive: true)));
        var category = await CreateCategoryAsync(handlers);
        var expense = await CreateExpenseAsync(
            handlers,
            category.Id,
            paymentMethod: PaymentMethod.CreditCard,
            cardId: cardId);

        var updated = await handlers.UpdateExpense().HandleAsync(
            new UpdateExpenseCommand(
                expense.Value.Id,
                200m,
                "TRY",
                OccurredOn,
                category.Id,
                "yeni",
                PaymentMethod.Cash,
                null,
                expense.Value.RowVersion),
            default);

        Assert.True(updated.IsSuccess);
        Assert.Equal(200m, updated.Value.Amount);
        Assert.Null(updated.Value.CardId);
        Assert.NotEqual(expense.Value.RowVersion, updated.Value.RowVersion);

        var stale = await handlers.UpdateExpense().HandleAsync(
            new UpdateExpenseCommand(
                expense.Value.Id,
                250m,
                "TRY",
                OccurredOn,
                category.Id,
                null,
                PaymentMethod.Cash,
                null,
                expense.Value.RowVersion),
            default);

        Assert.True(stale.IsFailure);
        Assert.Equal("ConcurrencyConflict", stale.Error.Code);
    }

    private static async Task<CategoryDto> CreateCategoryAsync(
        HandlerFactory handlers,
        string name = "Market")
    {
        var result = await handlers.CreateCategory()
            .HandleAsync(new CreateCategoryCommand(name, "cart"), default);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<Result<ExpenseDto>> CreateExpenseAsync(
        HandlerFactory handlers,
        Guid categoryId,
        decimal amount = 100m,
        PaymentMethod paymentMethod = PaymentMethod.Cash,
        Guid? cardId = null) =>
        handlers.CreateExpense().HandleAsync(
            new CreateExpenseCommand(
                amount,
                "TRY",
                OccurredOn,
                categoryId,
                "test",
                paymentMethod,
                cardId),
            default);
}
