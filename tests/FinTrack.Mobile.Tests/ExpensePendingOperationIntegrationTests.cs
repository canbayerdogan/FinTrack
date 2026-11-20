using System.Net;
using System.Net.Http.Json;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.ViewModels;
using FinTrack.Mobile.Tests.Fakes;
using Xunit;

namespace FinTrack.Mobile.Tests;

/// <summary>
/// V11-T02: gerçek AddExpenseViewModel.SaveAsync akışının pending-operation
/// altyapısıyla entegrasyonu. Testler servisleri doğrudan değil, ViewModel
/// üzerinden zinciri birlikte çalıştırır.
/// </summary>
public sealed class ExpensePendingOperationIntegrationTests
{
    private static readonly Uri BaseAddress = new("https://api.test/api/v1/");
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Save_post_oncesinde_pending_kaydi_olusturur_ve_idempotency_key_tasir()
    {
        TestRig? rig = null;
        var statesAtSend = new List<PendingOperationState?>();
        var queue = new QueueHandler(async (request, _) =>
        {
            var key = KeyOf(request);
            var operation = await rig!.Store.GetAsync(key);
            statesAtSend.Add(operation?.State);
            return Response(HttpStatusCode.Created);
        });
        rig = new TestRig(queue);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(PendingOperationState.SentUnconfirmed, Assert.Single(statesAtSend));
        Assert.Single(queue.Requests);
        Assert.Equal(HttpMethod.Post, queue.Requests[0].Method);
        Assert.EndsWith("/api/v1/expenses", queue.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.True(Guid.TryParse(KeyOf(queue.Requests[0]), out _));
        Assert.Equal(new[] { "//expenses" }, rig.Navigator.Routes);
        Assert.Null(await rig.Store.GetAsync(KeyOf(queue.Requests[0])));
    }

    [Fact]
    public async Task Ayni_kullanici_isleminin_tekrar_Save_cagrisi_yeni_anahtar_uretilmez()
    {
        var queue = new QueueHandler(
            Offline(), Offline(), Offline(),
            Ok(HttpStatusCode.Created));
        using var rig = new TestRig(queue, delay: NoDelay);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);
        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(4, queue.Requests.Count);
        var key = KeyOf(queue.Requests[0]);
        Assert.All(queue.Requests, request => Assert.Equal(key, KeyOf(request)));
        Assert.All(queue.Bodies, body => Assert.Equal(queue.Bodies[0], body));
        Assert.Equal(new[] { "//expenses" }, rig.Navigator.Routes);
        Assert.Empty(await rig.Store.ListForUserAsync(TestRig.UserId));
    }

    [Fact]
    public async Task Gonderim_sirasinda_process_bittiginde_sent_unconfirmed_kaydi_korunur()
    {
        var queue = new QueueHandler(Cancel(), Cancel(), Cancel());
        using var rig = new TestRig(queue, delay: NoDelay);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        var stored = Assert.Single(await rig.Store.ListForUserAsync(TestRig.UserId));
        Assert.Equal(PendingOperationState.SentUnconfirmed, stored.State);
        Assert.Equal(3, stored.AttemptCount);
        Assert.Equal(KeyOf(queue.Requests[0]), stored.Key);
        Assert.Equal(3, queue.Requests.Count);
        Assert.Empty(rig.Navigator.Routes);
        Assert.Contains("tekrar denenecek", rig.ViewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sunucu_tamamlandi_ancak_yanit_alinamadi_restart_ayni_key_ve_body_ile_kurtarir()
    {
        var firstQueue = new QueueHandler(Offline(), Offline(), Offline());
        using var firstRig = new TestRig(firstQueue, delay: NoDelay);
        await firstRig.ViewModel.SaveCommand.ExecuteAsync(null);
        var originalKey = KeyOf(firstQueue.Requests[0]);
        var originalBody = firstQueue.Bodies[0];

        var restartQueue = new QueueHandler(Ok(HttpStatusCode.Created));
        using var restartStore = new PendingOperationStore(
            firstRig.DatabasePath, firstRig.Secure, firstRig.Clock);
        var restartRecovery = new PendingOperationRecoveryService(
            new HttpClient(restartQueue) { BaseAddress = BaseAddress }, restartStore, firstRig.Clock);
        var restartStartup = new PendingOperationStartup(firstRig.Session, restartRecovery);

        var report = await restartStartup.RecoverCurrentUserAsync();

        var result = Assert.Single(report!.Results);
        Assert.Equal(RecoveryStatus.Completed, result.Status);
        Assert.Single(restartQueue.Requests);
        Assert.Equal(originalKey, KeyOf(restartQueue.Requests[0]));
        Assert.Equal(originalBody, Assert.Single(restartQueue.Bodies));
        Assert.Null(await restartStore.GetAsync(originalKey));
    }

    [Fact]
    public async Task Http401_ve_basari_refresh_sonrasi_ayni_key_ve_body_ile_gonderilir()
    {
        var refreshQueue = new QueueHandler(Ok(
            HttpStatusCode.OK,
            new { accessToken = "new-access", refreshToken = "new-refresh" }));
        var queue = new QueueHandler(Unauthorized(), Ok(HttpStatusCode.Created));
        using var rig = new TestRig(queue, useAuthHandler: true, refreshQueue: refreshQueue);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(2, queue.Requests.Count);
        Assert.Equal(KeyOf(queue.Requests[0]), KeyOf(queue.Requests[1]));
        Assert.Equal(queue.Bodies[0], queue.Bodies[1]);
        Assert.Equal("new-access", queue.Requests[1].Headers.Authorization?.Parameter);
        Assert.Equal(new[] { "//expenses" }, rig.Navigator.Routes);
        Assert.Empty(await rig.Store.ListForUserAsync(TestRig.UserId));
    }

    [Fact]
    public async Task Created_kaydi_baslangic_recovery_sinda_otomatik_gonderilmez()
    {
        var queue = new QueueHandler();
        using var rig = new TestRig(queue);
        await rig.Recovery.CreateAsync(TestRig.UserId, "POST", "/api/v1/expenses", [1, 2, 3]);

        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.NotNull(report);
        Assert.Empty(report!.Results);
        Assert.Empty(queue.Requests);
        var stored = Assert.Single(await rig.Store.ListForUserAsync(TestRig.UserId));
        Assert.Equal(PendingOperationState.Created, stored.State);
    }

    [Fact]
    public async Task S24_saat_sonrasi_durum_sorgusu_gercek_recovery_uzerinden_calisir()
    {
        var queue = new QueueHandler(Ok(
            HttpStatusCode.OK,
            new { status = "Completed", completedAtUtc = Start, hasBody = false }));
        using var rig = new TestRig(queue, clock: new FakeTimeProvider(Start));
        var operation = await rig.Recovery.CreateAsync(
            TestRig.UserId, "POST", "/api/v1/expenses", [1, 2, 3]);
        await rig.Store.SetStateAsync(operation.Key, PendingOperationState.SentUnconfirmed);
        rig.Clock.Advance(TimeSpan.FromHours(25));

        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.Equal(RecoveryStatus.Completed, Assert.Single(report!.Results).Status);
        Assert.Single(queue.Requests);
        Assert.Equal(HttpMethod.Get, queue.Requests[0].Method);
        Assert.EndsWith($"idempotency/{operation.Key}", queue.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Null(await rig.Store.GetAsync(operation.Key));
    }

    [Fact]
    public async Task S30_saat_sonrasi_durum_sorgusu_uncertain_olur_ve_yeni_islem_acilmaz()
    {
        // 24 s–30 g aralığında tam gövde yerine güvenli durum sorgusu kullanılır.
        var queue = new QueueHandler(Ok(HttpStatusCode.NotFound));
        using var rig = new TestRig(queue, clock: new FakeTimeProvider(Start));
        var operation = await rig.Recovery.CreateAsync(
            TestRig.UserId, "POST", "/api/v1/expenses", [1, 2, 3]);
        await rig.Store.SetStateAsync(operation.Key, PendingOperationState.SentUnconfirmed);
        rig.Clock.Advance(TimeSpan.FromHours(31));

        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.Equal(RecoveryStatus.Uncertain, Assert.Single(report!.Results).Status);
        Assert.Single(queue.Requests);
        Assert.Equal(HttpMethod.Get, queue.Requests[0].Method);
        Assert.EndsWith($"idempotency/{operation.Key}", queue.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        var stored = await rig.Store.GetAsync(operation.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.Uncertain, stored!.State);
    }

    [Fact]
    public async Task S30_gun_sonrasi_tombstone_uncertain_durumunu_korur_ve_yeni_islem_acmaz()
    {
        // V11-T01 tasarımında gövdesiz tombstone TTL'si 30 gündür (docs/11 §7.5).
        var queue = new QueueHandler();
        using var rig = new TestRig(queue, clock: new FakeTimeProvider(Start));
        var operation = await rig.Recovery.CreateAsync(
            TestRig.UserId, "POST", "/api/v1/expenses", [1, 2, 3]);
        await rig.Store.SetStateAsync(operation.Key, PendingOperationState.SentUnconfirmed);
        rig.Clock.Advance(TimeSpan.FromDays(31));

        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.Equal(RecoveryStatus.Uncertain, Assert.Single(report!.Results).Status);
        Assert.Empty(queue.Requests);
        var stored = await rig.Store.GetAsync(operation.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.Uncertain, stored!.State);
    }

    [Fact]
    public async Task Sifreleme_anahtari_kayboldugunda_kayit_silinmez()
    {
        var queue = new QueueHandler();
        using var rig = new TestRig(queue);
        var operation = await rig.Recovery.CreateAsync(
            TestRig.UserId, "POST", "/api/v1/expenses", [1, 2, 3]);
        await rig.Store.SetStateAsync(operation.Key, PendingOperationState.SentUnconfirmed);
        rig.Secure.Remove(PendingOperationKeyNames.ForUser(TestRig.UserId));

        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.Equal(RecoveryStatus.Uncertain, Assert.Single(report!.Results).Status);
        Assert.Empty(queue.Requests);
        var stored = await rig.Store.GetAsync(operation.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.Uncertain, stored!.State);
    }

    [Fact]
    public async Task Basarili_islemden_sonra_yeni_harcama_yeni_anahtarla_olusturulabilir()
    {
        var queue = new QueueHandler(
            Ok(HttpStatusCode.Created),
            Ok(HttpStatusCode.Created));
        using var rig = new TestRig(queue);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);
        var firstKey = KeyOf(queue.Requests[0]);
        rig.ViewModel.AmountText = "20,00";
        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(2, queue.Requests.Count);
        var secondKey = KeyOf(queue.Requests[1]);
        Assert.NotEqual(firstKey, secondKey);
        Assert.NotEqual(queue.Bodies[0], queue.Bodies[1]);
        Assert.Equal(2, rig.Navigator.Routes.Count);
        Assert.Empty(await rig.Store.ListForUserAsync(TestRig.UserId));
    }

    [Fact]
    public async Task Baska_kullanicinin_pending_kaydi_recovery_edilmez()
    {
        var queue = new QueueHandler(Ok(HttpStatusCode.Created));
        using var rig = new TestRig(queue);
        var other = await rig.Recovery.CreateAsync("other-user", "POST", "/api/v1/expenses", [1, 2, 3]);
        await rig.Store.SetStateAsync(other.Key, PendingOperationState.SentUnconfirmed);

        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.NotNull(report);
        Assert.Empty(report!.Results);
        Assert.Empty(queue.Requests);
        var stored = await rig.Store.GetAsync(other.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.SentUnconfirmed, stored!.State);
    }

    [Fact]
    public async Task Oturum_degisirse_pending_kayit_baska_token_ile_gonderilmez()
    {
        var queue = new QueueHandler(Ok(HttpStatusCode.Created));
        using var rig = new TestRig(queue);
        var operation = await rig.Recovery.CreateAsync(
            TestRig.UserId, "POST", "/api/v1/expenses", [1, 2, 3]);
        await rig.Store.SetStateAsync(operation.Key, PendingOperationState.SentUnconfirmed);
        await rig.Tokens.SaveAsync(new TokenPair(FakeJwt.WithSubject("other-user"), "refresh"));

        var result = await rig.Recovery.RecoverOneAsync(operation.Key);

        Assert.Equal(RecoveryStatus.Pending, result.Status);
        Assert.Contains("Oturum değişti", result.Message, StringComparison.Ordinal);
        Assert.Empty(queue.Requests);
        var stored = await rig.Store.GetAsync(operation.Key);
        Assert.NotNull(stored);
        Assert.Equal(PendingOperationState.SentUnconfirmed, stored!.State);
    }

    [Fact]
    public async Task JWT_sub_olmayan_oturumda_pending_kayit_ve_recovery_olusz()
    {
        var queue = new QueueHandler(Ok(HttpStatusCode.Created));
        using var rig = new TestRig(queue);
        await rig.Tokens.SaveAsync(new TokenPair("not-a-jwt", "refresh"));

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);
        var report = await rig.Startup.RecoverCurrentUserAsync();

        Assert.Empty(queue.Requests);
        Assert.Empty(await rig.Store.ListForUserAsync(TestRig.UserId));
        Assert.Null(report);
        Assert.Contains("Oturum bilgisi", rig.ViewModel.ErrorMessage, StringComparison.Ordinal);
    }

    private static string KeyOf(HttpRequestMessage request) =>
        request.Headers.GetValues("Idempotency-Key").Single();

    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    private static HttpResponseMessage Response(HttpStatusCode status, object? body = null)
    {
        var response = new HttpResponseMessage(status);
        if (body is not null)
        {
            response.Content = JsonContent.Create(body, options: ApiJson.Options);
        }

        return response;
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Ok(
        HttpStatusCode status,
        object? body = null) =>
        (_, _) => Task.FromResult(Response(status, body));

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Unauthorized() =>
        Ok(
            HttpStatusCode.Unauthorized,
            new { status = 401, code = "InvalidToken", title = "Oturum süresi doldu" });

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Offline() =>
        (_, _) => throw new HttpRequestException("offline");

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Cancel() =>
        (_, _) => throw new TaskCanceledException("process terminated");

    private sealed class TestRig : IDisposable
    {
        public const string UserId = "user-a";

        private readonly TempDatabase _database = new();

        public TestRig(
            QueueHandler queue,
            FakeTimeProvider? clock = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null,
            bool useAuthHandler = false,
            QueueHandler? refreshQueue = null)
        {
            Clock = clock ?? new FakeTimeProvider(DateTimeOffset.UtcNow);
            Secure = new FakeSecureKeyValueStore();
            Tokens = new FakeTokenStore();
            Tokens.SaveAsync(new TokenPair(FakeJwt.WithSubject(UserId), "refresh")).GetAwaiter().GetResult();
            Store = new PendingOperationStore(_database.DatabasePath, Secure, Clock);

            HttpClient recoveryHttp;
            if (useAuthHandler)
            {
                var refresher = new SessionRefresher(
                    new HttpClient(refreshQueue ?? new QueueHandler()) { BaseAddress = BaseAddress }, Tokens);
                var auth = new AuthHandler(Tokens, refresher, Store) { InnerHandler = queue };
                recoveryHttp = new HttpClient(auth) { BaseAddress = BaseAddress };
            }
            else
            {
                recoveryHttp = new HttpClient(queue) { BaseAddress = BaseAddress };
            }

            var api = new FinTrackApiClient(
                new HttpClient(new QueueHandler()) { BaseAddress = BaseAddress });
            Session = new SessionService(api, Tokens);
            Recovery = new PendingOperationRecoveryService(
                recoveryHttp,
                Store,
                Clock,
                delay,
                currentUserKeyProvider: Session.GetUserIdKeyAsync);
            Startup = new PendingOperationStartup(Session, Recovery);
            ViewModel = new AddExpenseViewModel(api, Navigator, Recovery, Session)
            {
                AmountText = "12,50",
                SelectedCategory = new CategoryDto(Guid.NewGuid(), "Market", null, false, "AAAAAAAAAAA="),
                PaymentMethod = "Cash",
            };
        }

        public string DatabasePath => _database.DatabasePath;

        public FakeTimeProvider Clock { get; }

        public FakeSecureKeyValueStore Secure { get; }

        public FakeTokenStore Tokens { get; }

        public PendingOperationStore Store { get; }

        public PendingOperationRecoveryService Recovery { get; }

        public SessionService Session { get; }

        public PendingOperationStartup Startup { get; }

        public AddExpenseViewModel ViewModel { get; }

        public FakeNavigator Navigator { get; } = new();

        public void Dispose()
        {
            Store.Dispose();
            _database.Dispose();
        }
    }
}
