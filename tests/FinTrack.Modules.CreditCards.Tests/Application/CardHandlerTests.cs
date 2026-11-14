using FinTrack.Modules.CreditCards.Application.Cards;
using FinTrack.Modules.CreditCards.Domain;
using FinTrack.Modules.CreditCards.Tests.Support;
using Xunit;

namespace FinTrack.Modules.CreditCards.Tests.Application;

public sealed class CardHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Kart_olusturulur_getirilir_ve_listelenir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);

        var create = await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Örnek Bank", "Platinum", "ornek-bank", 50_000m, 25, 5),
            CancellationToken.None);

        Assert.True(create.IsSuccess);
        var created = create.Value;
        Assert.Equal(CardStatus.Active, created.Status);
        Assert.Equal(8, Convert.FromBase64String(created.RowVersion).Length);

        var fetched = await factory.GetCard().HandleAsync(new GetCardQuery(created.Id), CancellationToken.None);
        Assert.True(fetched.IsSuccess);
        Assert.Equal("Platinum", fetched.Value.DisplayName);

        var list = await factory.ListCards().HandleAsync(
            new ListCardsQuery(new CardFilter(null)),
            CancellationToken.None);
        Assert.True(list.IsSuccess);
        Assert.Single(list.Value);
    }

    [Fact]
    public async Task Guncelleme_rowVersion_ister_ve_eski_surumu_reddeder()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        Guid cardId;
        string firstRowVersion;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CardHandlerFactory(db, UserId, Now);
            var created = (await factory.CreateCard().HandleAsync(
                new CreateCardCommand("Banka", "Kart", null, null, null, null),
                CancellationToken.None)).Value;
            cardId = created.Id;
            firstRowVersion = created.RowVersion;
        }

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CardHandlerFactory(db, UserId, Now);

            var missing = await factory.UpdateCard().HandleAsync(
                new UpdateCardCommand(cardId, "Banka", "Kart", null, null, null, null, null),
                CancellationToken.None);
            Assert.Equal("RowVersionInvalid", missing.Error.Code);

            var update = await factory.UpdateCard().HandleAsync(
                new UpdateCardCommand(cardId, "Yeni Banka", "Yeni Kart", null, 10_000m, 15, 25, firstRowVersion),
                CancellationToken.None);
            Assert.True(update.IsSuccess);
            Assert.Equal("Yeni Banka", update.Value.BankName);
            Assert.NotEqual(firstRowVersion, update.Value.RowVersion);

            var stale = await factory.UpdateCard().HandleAsync(
                new UpdateCardCommand(cardId, "Eski", "Eski", null, null, null, null, firstRowVersion),
                CancellationToken.None);
            Assert.Equal("ConcurrencyConflict", stale.Error.Code);
        }
    }

    [Fact]
    public async Task Arsivleme_karti_gizler_ve_arsivli_kart_okunabilir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);

        var created = (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", "Kart", null, null, null, null),
            CancellationToken.None)).Value;

        var archive = await factory.ArchiveCard().HandleAsync(
            new ArchiveCardCommand(created.Id, created.RowVersion),
            CancellationToken.None);
        Assert.True(archive.IsSuccess);

        var defaultList = await factory.ListCards().HandleAsync(
            new ListCardsQuery(new CardFilter(null)),
            CancellationToken.None);
        Assert.Empty(defaultList.Value);

        var archivedList = await factory.ListCards().HandleAsync(
            new ListCardsQuery(new CardFilter(CardStatus.Archived)),
            CancellationToken.None);
        Assert.Single(archivedList.Value);

        var fetched = await factory.GetCard().HandleAsync(new GetCardQuery(created.Id), CancellationToken.None);
        Assert.True(fetched.IsSuccess);
        Assert.Equal(CardStatus.Archived, fetched.Value.Status);

        // Arşivli kartın geçmiş kayıtları korunur; bakiyesi okunabilir.
        factory.ExpenseQuery.Totals = new(1200m, 200m);
        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(created.Id),
            CancellationToken.None);
        Assert.True(balance.IsSuccess);
        Assert.Equal(1000m, balance.Value.Debt);
    }

    [Fact]
    public async Task Bakiye_harcama_eksi_iade_ve_sifir_odeme_avans_ile_hesaplanir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);

        var created = (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", "Kart", null, null, null, null),
            CancellationToken.None)).Value;

        factory.ExpenseQuery.Totals = new(12_000m, 800m);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(created.Id),
            CancellationToken.None);

        Assert.True(balance.IsSuccess);
        var value = balance.Value;
        Assert.Equal(11_200m, value.Debt);
        Assert.False(value.IsInCredit);
        Assert.Equal("TRY", value.Currency);
        Assert.Equal(Now, value.AsOfUtc);
        Assert.Equal(12_000m, value.Breakdown.Expenses);
        Assert.Equal(800m, value.Breakdown.Refunds);
        Assert.Equal(0m, value.Breakdown.CashAdvances);
        Assert.Equal(0m, value.Breakdown.Payments);
        Assert.Equal(created.Id, factory.ExpenseQuery.LastCardId);
    }

    [Fact]
    public async Task Bakiye_negatifse_kart_alacagi_olarak_isaretlenir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);

        var created = (await factory.CreateCard().HandleAsync(
            new CreateCardCommand("Banka", "Kart", null, null, null, null),
            CancellationToken.None)).Value;

        // Slice 3'te geçerli işlemlerle negatif bakiye oluşamaz (iade harcamayı
        // aşamaz). Fazla ödeme slice 4'te gelecek; türetim burada doğrulanır.
        factory.ExpenseQuery.Totals = new(100m, 150m);

        var balance = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(created.Id),
            CancellationToken.None);

        Assert.True(balance.IsSuccess);
        Assert.Equal(-50m, balance.Value.Debt);
        Assert.True(balance.Value.IsInCredit);
    }

    [Fact]
    public async Task Kullanicilar_birbirinin_kartina_erisemez()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        var otherUser = Guid.NewGuid();
        Guid cardId;
        string rowVersion;

        await using (var db = database.CreateContext(UserId))
        {
            var factory = new CardHandlerFactory(db, UserId, Now);
            var created = (await factory.CreateCard().HandleAsync(
                new CreateCardCommand("Banka", "Kart", null, null, null, null),
                CancellationToken.None)).Value;
            cardId = created.Id;
            rowVersion = created.RowVersion;
        }

        await using (var db = database.CreateContext(otherUser))
        {
            var factory = new CardHandlerFactory(db, otherUser, Now);

            var fetched = await factory.GetCard().HandleAsync(new GetCardQuery(cardId), CancellationToken.None);
            Assert.Equal("NotFound", fetched.Error.Code);

            var update = await factory.UpdateCard().HandleAsync(
                new UpdateCardCommand(cardId, "Banka", "Kart", null, null, null, null, rowVersion),
                CancellationToken.None);
            Assert.Equal("NotFound", update.Error.Code);

            var archive = await factory.ArchiveCard().HandleAsync(
                new ArchiveCardCommand(cardId, rowVersion),
                CancellationToken.None);
            Assert.Equal("NotFound", archive.Error.Code);

            var list = await factory.ListCards().HandleAsync(
                new ListCardsQuery(new CardFilter(null)),
                CancellationToken.None);
            Assert.Empty(list.Value);
        }
    }

    [Fact]
    public async Task Bilinmeyen_kart_bakiyesi_404_uretir()
    {
        await using var database = await CardTestDatabase.CreateAsync();
        await using var db = database.CreateContext(UserId);
        var factory = new CardHandlerFactory(db, UserId, Now);

        var result = await factory.GetCardBalance().HandleAsync(
            new GetCardBalanceQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal("NotFound", result.Error.Code);
    }
}
