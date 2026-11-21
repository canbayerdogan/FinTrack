using System.Net;
using System.Net.Http.Json;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Core.ViewModels;
using FinTrack.Mobile.Tests.Fakes;
using Xunit;

namespace FinTrack.Mobile.Tests;

public sealed class ViewModelTests
{
    private static readonly Uri BaseAddress = new("https://api.test/api/v1/");

    [Fact]
    public async Task Giris_bos_eposta_ile_istek_gondermez()
    {
        var queue = new QueueHandler();
        var (session, navigator, store) = CreateServices(queue);
        var viewModel = new LoginViewModel(session, navigator) { Email = string.Empty, Password = "abc12345" };

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("E-posta zorunludur.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
        Assert.Empty(navigator.Routes);
    }

    [Fact]
    public async Task Giris_yanlis_credential_mesajini_gosterir()
    {
        var queue = new QueueHandler(QueueHandler.Json(
            HttpStatusCode.Unauthorized,
            new
            {
                title = "Kimlik doğrulama hatası",
                status = 401,
                code = "InvalidCredentials",
                detail = "E-posta veya parola hatalı.",
            }));
        var (session, navigator, _) = CreateServices(queue);
        var viewModel = new LoginViewModel(session, navigator)
        {
            Email = "user@example.com",
            Password = "abc12345",
        };

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("E-posta veya şifre hatalı.", viewModel.ErrorMessage);
        Assert.Empty(navigator.Routes);
    }

    [Fact]
    public async Task Giris_basarili_oldugunda_token_saklanir_ve_ana_sayfaya_gidilir()
    {
        var queue = new QueueHandler(QueueHandler.Json(HttpStatusCode.OK, AuthJson("access", "refresh")));
        var (session, navigator, store) = CreateServices(queue);
        var viewModel = new LoginViewModel(session, navigator)
        {
            Email = "user@example.com",
            Password = "abc12345",
        };

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal("access", store.AccessToken);
        Assert.Equal("refresh", store.RefreshToken);
        Assert.Equal(new[] { "//main" }, navigator.Routes);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Kayit_parolalar_uyusmazsa_istek_gondermez()
    {
        var queue = new QueueHandler();
        var (session, navigator, _) = CreateServices(queue);
        var viewModel = new RegisterViewModel(session, navigator)
        {
            Email = "user@example.com",
            Password = "abc12345",
            ConfirmPassword = "abc123456",
            PreferredTimeZone = "Europe/Istanbul",
        };

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Parolalar eşleşmiyor.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Harcama_ekleme_gecersiz_tutar_için_istek_gondermez()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        var navigator = new FakeNavigator();
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var recovery = new PendingOperationRecoveryService(
            new HttpClient(new QueueHandler()) { BaseAddress = BaseAddress }, store);
        var session = new Core.Services.SessionService(api, new FakeTokenStore());
        var viewModel = new AddExpenseViewModel(api, navigator, recovery, session) { AmountText = "abc" };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Tutar sıfırdan büyük bir sayı olmalıdır.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Harcama_ekleme_kart_secilmeden_kredi_karti_bilgisi_vermez()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        using var database = new TempDatabase();
        using var store = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var recovery = new PendingOperationRecoveryService(
            new HttpClient(new QueueHandler()) { BaseAddress = BaseAddress }, store);
        var session = new Core.Services.SessionService(api, new FakeTokenStore());
        var viewModel = new AddExpenseViewModel(api, new FakeNavigator(), recovery, session)
        {
            AmountText = "12,50",
            SelectedCategory = new CategoryDto(Guid.NewGuid(), "Market", null, false, "AAAAAAAAAAA="),
            PaymentMethod = "CreditCard",
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Kart seçiniz.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Raporlar_gecersiz_ay_için_apiye_basmaz()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        var viewModel = new ReportsViewModel(api) { Month = 13 };

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.Contains("ay", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Ayarlar_gecersiz_saat_dilimini_kaydetmez()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        var (session, navigator, store) = CreateServices(queue);
        var viewModel = new SettingsViewModel(api, session, navigator)
        {
            PreferredTimeZone = "Mars/Phobos",
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Contains("IANA", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Kart_detayi_id_olmadan_hata_verir()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        var viewModel = new CardDetailViewModel(api);

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.Equal("Kart seçilmedi.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Kartlar_ekle_komutu_kart_ekleme_sayfasina_gider()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        var navigator = new FakeNavigator();
        var viewModel = new CardsViewModel(api, navigator);

        await viewModel.AddCardCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "addcard" }, navigator.Routes);
        Assert.Empty(queue.Requests);
    }

    [Fact]
    public async Task Kart_ekleme_banka_adi_olmadan_istek_gondermez()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        var navigator = new FakeNavigator();
        using var database = new TempDatabase();
        using var pendingStore = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var recovery = new PendingOperationRecoveryService(
            new HttpClient(new QueueHandler()) { BaseAddress = BaseAddress }, pendingStore);
        var session = new Core.Services.SessionService(api, new FakeTokenStore());
        var viewModel = new AddCardViewModel(navigator, recovery, session) { DisplayName = "Axess kredi kartı" };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Banka adı zorunludur.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
        Assert.Empty(navigator.Routes);
    }

    [Fact]
    public async Task Kart_ekleme_gecersiz_ekstre_gunu_ile_istek_gondermez()
    {
        var queue = new QueueHandler();
        var api = new FinTrackApiClient(new HttpClient(queue) { BaseAddress = BaseAddress });
        using var database = new TempDatabase();
        using var pendingStore = new PendingOperationStore(database.DatabasePath, new FakeSecureKeyValueStore());
        var recovery = new PendingOperationRecoveryService(
            new HttpClient(new QueueHandler()) { BaseAddress = BaseAddress }, pendingStore);
        var session = new Core.Services.SessionService(api, new FakeTokenStore());
        var viewModel = new AddCardViewModel(new FakeNavigator(), recovery, session)
        {
            BankName = "Akbank",
            DisplayName = "Axess kredi kartı",
            StatementDayText = "32",
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Ekstre günü 1 ile 31 arasında olmalıdır.", viewModel.ErrorMessage);
        Assert.Empty(queue.Requests);
    }

    private static (Core.Services.SessionService Session, FakeNavigator Navigator, FakeTokenStore Store) CreateServices(
        HttpMessageHandler handler)
    {
        var api = new FinTrackApiClient(new HttpClient(handler) { BaseAddress = BaseAddress });
        var store = new FakeTokenStore();
        return (new Core.Services.SessionService(api, store), new FakeNavigator(), store);
    }

    private static object AuthJson(string accessToken, string refreshToken) => new
    {
        userId = Guid.NewGuid(),
        email = "user@example.com",
        preferredTimeZone = "Europe/Istanbul",
        accessToken,
        refreshToken,
        expiresIn = 900,
        tokenType = "Bearer",
    };
}
