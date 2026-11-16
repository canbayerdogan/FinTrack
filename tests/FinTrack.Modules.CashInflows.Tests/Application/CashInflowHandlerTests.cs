using FinTrack.Modules.CashInflows.Application.CashInflows;
using FinTrack.Modules.CashInflows.Domain;
using FinTrack.Modules.CashInflows.Tests.Support;
using Xunit;

namespace FinTrack.Modules.CashInflows.Tests.Application;

public sealed class CashInflowHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 14);
    private static readonly DateOnly OccurredOn = Today.AddDays(-2);

    [Fact]
    public async Task Olusturma_getirme_ve_listeleme()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        var create = await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(80_000m, "try", OccurredOn, CashInflowSource.Salary, "  Kasım maaşı  "),
            CancellationToken.None);

        Assert.True(create.IsSuccess);
        var created = create.Value;
        Assert.Equal(80_000m, created.Amount);
        Assert.Equal("TRY", created.Currency);
        Assert.Equal("Kasım maaşı", created.Description);
        Assert.True(created.IsEarnedIncome);
        Assert.Equal(CashInflowStatus.Active, created.Status);
        Assert.Null(created.DebtLink);
        Assert.Equal(8, Convert.FromBase64String(created.RowVersion).Length);

        var fetched = await factory.GetCashInflow().HandleAsync(
            new GetCashInflowQuery(created.Id),
            CancellationToken.None);
        Assert.True(fetched.IsSuccess);
        Assert.Equal(created.Id, fetched.Value.Id);
        Assert.Equal(created.RowVersion, fetched.Value.RowVersion);

        var list = await factory.ListCashInflows().HandleAsync(
            new ListCashInflowsQuery(new CashInflowFilter(null, null, null, null, false, 1, 50)),
            CancellationToken.None);
        Assert.True(list.IsSuccess);
        Assert.Equal(1, list.Value.TotalCount);
        Assert.Single(list.Value.Items);
    }

    [Fact]
    public async Task Kaynak_zorunludur()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        var result = await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(100m, "TRY", OccurredOn, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("CashInflowSourceRequired", result.Error.Code);
    }

    [Theory]
    [InlineData(CashInflowSource.BankLoan)]
    [InlineData(CashInflowSource.CashAdvance)]
    [InlineData(CashInflowSource.PersonalBorrowing)]
    public async Task Borc_doguran_kaynak_dogrudan_olusturulamaz(CashInflowSource source)
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        var result = await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(100m, "TRY", OccurredOn, source, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DebtSourceRequiresOwnerEndpoint", result.Error.Code);
    }

    [Fact]
    public async Task Unknown_kaynak_gelir_niteligi_null_doner()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        var result = await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(500m, "TRY", OccurredOn, CashInflowSource.Unknown, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.IsEarnedIncome);
        Assert.Null(result.Value.DebtLink);
    }

    [Fact]
    public async Task AssetSale_gelir_degildir()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        var result = await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(1_000m, "TRY", OccurredOn, CashInflowSource.AssetSale, "Araba satışı"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsEarnedIncome);
    }

    [Fact]
    public async Task Guncelleme_rowVersion_ister_ve_eski_surumu_reddeder()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        Guid inflowId;
        string firstRowVersion;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CashInflowHandlerFactory(db, UserId, Now);
            var created = (await factory.CreateCashInflow().HandleAsync(
                new CreateCashInflowCommand(100m, "TRY", OccurredOn, CashInflowSource.Salary, null),
                CancellationToken.None)).Value;
            inflowId = created.Id;
            firstRowVersion = created.RowVersion;
        }

        string updatedRowVersion;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CashInflowHandlerFactory(db, UserId, Now);

            var missing = await factory.UpdateCashInflow().HandleAsync(
                new UpdateCashInflowCommand(inflowId, 200m, "TRY", Today.AddDays(-1), CashInflowSource.Salary, null, null),
                CancellationToken.None);
            Assert.Equal("RowVersionInvalid", missing.Error.Code);

            var update = await factory.UpdateCashInflow().HandleAsync(
                new UpdateCashInflowCommand(
                    inflowId,
                    200m,
                    "TRY",
                    Today.AddDays(-1),
                    CashInflowSource.BusinessIncome,
                    "Güncel",
                    firstRowVersion),
                CancellationToken.None);

            Assert.True(update.IsSuccess);
            Assert.Equal(200m, update.Value.Amount);
            Assert.Equal(CashInflowSource.BusinessIncome, update.Value.Source);
            Assert.True(update.Value.IsEarnedIncome);
            Assert.NotEqual(firstRowVersion, update.Value.RowVersion);
            updatedRowVersion = update.Value.RowVersion;
        }

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CashInflowHandlerFactory(db, UserId, Now);

            var stale = await factory.UpdateCashInflow().HandleAsync(
                new UpdateCashInflowCommand(inflowId, 300m, "TRY", OccurredOn, CashInflowSource.Salary, null, firstRowVersion),
                CancellationToken.None);
            Assert.Equal("ConcurrencyConflict", stale.Error.Code);

            // Güncel sürümle güncelleme başarılıdır.
            var current = await factory.UpdateCashInflow().HandleAsync(
                new UpdateCashInflowCommand(
                    inflowId,
                    300m,
                    "TRY",
                    OccurredOn,
                    CashInflowSource.Salary,
                    null,
                    updatedRowVersion),
                CancellationToken.None);
            Assert.True(current.IsSuccess);
        }
    }

    [Fact]
    public async Task Iptal_gerekcesiz_reddedilir_idempotenttir_ve_guncellemeyi_kapatir()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        var created = (await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(100m, "TRY", OccurredOn, CashInflowSource.Salary, null),
            CancellationToken.None)).Value;

        var missingReason = await factory.CancelCashInflow().HandleAsync(
            new CancelCashInflowCommand(created.Id, null, created.RowVersion),
            CancellationToken.None);
        Assert.Equal("CancellationReasonRequired", missingReason.Error.Code);

        var cancel = await factory.CancelCashInflow().HandleAsync(
            new CancelCashInflowCommand(created.Id, "Yanlış giriş", created.RowVersion),
            CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        var cancelled = await factory.GetCashInflow().HandleAsync(
            new GetCashInflowQuery(created.Id),
            CancellationToken.None);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(CashInflowStatus.Cancelled, cancelled.Value.Status);

        var cancelAgain = await factory.CancelCashInflow().HandleAsync(
            new CancelCashInflowCommand(created.Id, "Tekrar", cancelled.Value.RowVersion),
            CancellationToken.None);
        Assert.True(cancelAgain.IsSuccess);

        var updateCancelled = await factory.UpdateCashInflow().HandleAsync(
            new UpdateCashInflowCommand(created.Id, 200m, "TRY", OccurredOn, CashInflowSource.Salary, null, cancelled.Value.RowVersion),
            CancellationToken.None);
        Assert.Equal("StateConflict", updateCancelled.Error.Code);
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_kaydina_erisemez()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        var otherUser = Guid.NewGuid();
        Guid inflowId;
        string rowVersion;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CashInflowHandlerFactory(db, UserId, Now);
            var created = (await factory.CreateCashInflow().HandleAsync(
                new CreateCashInflowCommand(100m, "TRY", OccurredOn, CashInflowSource.Salary, null),
                CancellationToken.None)).Value;
            inflowId = created.Id;
            rowVersion = created.RowVersion;
        }

        await using (var db = database.CreateContext(otherUser))
        {
            var factory = new CashInflowHandlerFactory(db, otherUser, Now);

            var fetched = await factory.GetCashInflow().HandleAsync(
                new GetCashInflowQuery(inflowId),
                CancellationToken.None);
            Assert.Equal("NotFound", fetched.Error.Code);

            var update = await factory.UpdateCashInflow().HandleAsync(
                new UpdateCashInflowCommand(inflowId, 200m, "TRY", OccurredOn, CashInflowSource.Salary, null, rowVersion),
                CancellationToken.None);
            Assert.Equal("NotFound", update.Error.Code);

            var cancel = await factory.CancelCashInflow().HandleAsync(
                new CancelCashInflowCommand(inflowId, "İptal", rowVersion),
                CancellationToken.None);
            Assert.Equal("NotFound", cancel.Error.Code);

            var list = await factory.ListCashInflows().HandleAsync(
                new ListCashInflowsQuery(new CashInflowFilter(null, null, null, null, true, 1, 50)),
                CancellationToken.None);
            Assert.True(list.IsSuccess);
            Assert.Equal(0, list.Value.TotalCount);
        }
    }

    [Fact]
    public async Task Borc_bagli_kayit_guncellenemez_ve_iptal_edilemez()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);
        var debtLinkId = Guid.NewGuid();

        await factory.SeedDebtLinkedAsync(15_000m, OccurredOn, CashInflowSource.CashAdvance, debtLinkId);

        var list = await factory.ListCashInflows().HandleAsync(
            new ListCashInflowsQuery(new CashInflowFilter(null, null, null, null, false, 1, 50)),
            CancellationToken.None);
        Assert.True(list.IsSuccess);
        var debtLinked = Assert.Single(list.Value.Items);
        Assert.NotNull(debtLinked.DebtLink);
        Assert.Equal(DebtLinkType.CashAdvance, debtLinked.DebtLink!.Type);
        Assert.Equal(debtLinkId, debtLinked.DebtLink.Id);
        Assert.False(debtLinked.IsEarnedIncome);

        var update = await factory.UpdateCashInflow().HandleAsync(
            new UpdateCashInflowCommand(
                debtLinked.Id,
                1m,
                "TRY",
                OccurredOn,
                CashInflowSource.CashAdvance,
                null,
                debtLinked.RowVersion),
            CancellationToken.None);
        Assert.Equal("StateConflict", update.Error.Code);

        var cancel = await factory.CancelCashInflow().HandleAsync(
            new CancelCashInflowCommand(debtLinked.Id, "İptal", debtLinked.RowVersion),
            CancellationToken.None);
        Assert.Equal("StateConflict", cancel.Error.Code);

        var current = await factory.GetCashInflow().HandleAsync(
            new GetCashInflowQuery(debtLinked.Id),
            CancellationToken.None);
        Assert.True(current.IsSuccess);
        Assert.Equal(CashInflowStatus.Active, current.Value.Status);
    }

    [Fact]
    public async Task Liste_filtreleri_ve_sayfalama_calisir()
    {
        await using var database = await CashInflowTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CashInflowHandlerFactory(db, UserId, Now);

        await CreateAsync(factory, 10m, Today.AddDays(-3), CashInflowSource.Salary);
        await CreateAsync(factory, 20m, Today.AddDays(-2), CashInflowSource.AssetSale);
        await CreateAsync(factory, 30m, Today.AddDays(-1), CashInflowSource.BusinessIncome);

        var cancelled = await CreateAsync(factory, 40m, Today.AddDays(-1), CashInflowSource.Unknown);
        var cancel = await factory.CancelCashInflow().HandleAsync(
            new CancelCashInflowCommand(cancelled.Id, "Yanlış giriş", cancelled.RowVersion),
            CancellationToken.None);
        Assert.True(cancel.IsSuccess);

        var defaultList = await ListAsync(factory, new CashInflowFilter(null, null, null, null, false, 1, 50));
        Assert.Equal(3, defaultList.TotalCount);
        Assert.DoesNotContain(defaultList.Items, item => item.Status == CashInflowStatus.Cancelled);
        Assert.Equal(Today.AddDays(-1), defaultList.Items[0].OccurredOn);

        var withCancelled = await ListAsync(factory, new CashInflowFilter(null, null, null, null, true, 1, 50));
        Assert.Equal(4, withCancelled.TotalCount);

        var cancelledOnly = await ListAsync(
            factory,
            new CashInflowFilter(null, null, null, CashInflowStatus.Cancelled, false, 1, 50));
        Assert.Equal(1, cancelledOnly.TotalCount);
        Assert.Equal(CashInflowSource.Unknown, cancelledOnly.Items[0].Source);
        Assert.Null(cancelledOnly.Items[0].IsEarnedIncome);

        var activeOnly = await ListAsync(
            factory,
            new CashInflowFilter(null, null, null, CashInflowStatus.Active, false, 1, 50));
        Assert.Equal(3, activeOnly.TotalCount);

        var bySource = await ListAsync(
            factory,
            new CashInflowFilter(null, null, CashInflowSource.AssetSale, null, false, 1, 50));
        Assert.Equal(1, bySource.TotalCount);
        Assert.Equal(CashInflowSource.AssetSale, bySource.Items[0].Source);

        var from = await ListAsync(
            factory,
            new CashInflowFilter(Today.AddDays(-2), null, null, null, false, 1, 50));
        Assert.Equal(2, from.TotalCount);

        var to = await ListAsync(
            factory,
            new CashInflowFilter(null, Today.AddDays(-3), null, null, false, 1, 50));
        Assert.Equal(1, to.TotalCount);
        Assert.Equal(CashInflowSource.Salary, to.Items[0].Source);

        var secondPage = await ListAsync(factory, new CashInflowFilter(null, null, null, null, false, 2, 1));
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(3, secondPage.TotalPages);
        var secondPageItem = Assert.Single(secondPage.Items);
        Assert.Equal(CashInflowSource.AssetSale, secondPageItem.Source);

        var firstPage = await ListAsync(factory, new CashInflowFilter(null, null, null, null, false, 1, 2));
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(CashInflowSource.BusinessIncome, firstPage.Items[0].Source);
    }

    private static async Task<CashInflowDto> CreateAsync(
        CashInflowHandlerFactory factory,
        decimal amount,
        DateOnly occurredOn,
        CashInflowSource source)
    {
        var result = await factory.CreateCashInflow().HandleAsync(
            new CreateCashInflowCommand(amount, "TRY", occurredOn, source, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<FinTrack.Modules.CashInflows.Application.Common.PagedResult<CashInflowDto>> ListAsync(
        CashInflowHandlerFactory factory,
        CashInflowFilter filter)
    {
        var result = await factory.ListCashInflows().HandleAsync(
            new ListCashInflowsQuery(filter),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
