using System.Net;
using System.Text.Json;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.PendingOperations;
using FinTrack.Mobile.Core.Services;
using FinTrack.Mobile.Core.ViewModels;
using FinTrack.Mobile.Tests.Fakes;
using Xunit;

namespace FinTrack.Mobile.Tests;

/// <summary>
/// POST /cards idempotency kapsamındadır; kart ekleme akışının pending-operation
/// zincirini ve gövde sözleşmesini birlikte doğrular.
/// </summary>
public sealed class CardPendingOperationIntegrationTests
{
    private static readonly Uri BaseAddress = new("https://api.test/api/v1/");

    [Fact]
    public async Task Kart_ekleme_pending_kaydi_ve_idempotency_key_ile_gonderilir()
    {
        var queue = new QueueHandler(Created());
        using var rig = new TestRig(queue);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        var request = Assert.Single(queue.Requests);
        var key = KeyOf(request);
        Assert.True(Guid.TryParse(key, out _));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/cards", request.RequestUri!.AbsolutePath);
        Assert.Equal(new[] { "//cards" }, rig.Navigator.Routes);
        Assert.Null(rig.ViewModel.ErrorMessage);
        Assert.Null(await rig.Store.GetAsync(key));
    }

    [Fact]
    public async Task Kart_ekleme_govdesi_sadece_ad_ve_opsiyonel_alanlari_icerir()
    {
        var queue = new QueueHandler(Created());
        using var rig = new TestRig(queue);

        await rig.ViewModel.SaveCommand.ExecuteAsync(null);

        using var document = JsonDocument.Parse(Assert.Single(queue.Bodies)!);
        var body = document.RootElement;
        Assert.Equal("Akbank", body.GetProperty("bankName").GetString());
        Assert.Equal("Axess kredi kartı", body.GetProperty("displayName").GetString());
        Assert.Equal(50000.50m, body.GetProperty("creditLimit").GetDecimal());
        Assert.Equal(25, body.GetProperty("statementDay").GetInt32());
        Assert.Equal(5, body.GetProperty("paymentDueDay").GetInt32());
        Assert.False(body.TryGetProperty("cardNumber", out _));
        Assert.False(body.TryGetProperty("cvv", out _));
        Assert.False(body.TryGetProperty("expiryDate", out _));
    }

    private static string KeyOf(HttpRequestMessage request) =>
        request.Headers.GetValues("Idempotency-Key").Single();

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Created() =>
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));

    private sealed class TestRig : IDisposable
    {
        public const string UserId = "user-a";

        private readonly TempDatabase _database = new();

        public TestRig(QueueHandler queue)
        {
            Tokens = new FakeTokenStore();
            Tokens.SaveAsync(new TokenPair(FakeJwt.WithSubject(UserId), "refresh"))
                .GetAwaiter()
                .GetResult();
            Store = new PendingOperationStore(_database.DatabasePath, new FakeSecureKeyValueStore());

            var api = new FinTrackApiClient(
                new HttpClient(new QueueHandler()) { BaseAddress = BaseAddress });
            Session = new SessionService(api, Tokens);
            Recovery = new PendingOperationRecoveryService(
                new HttpClient(queue) { BaseAddress = BaseAddress },
                Store,
                currentUserKeyProvider: Session.GetUserIdKeyAsync);
            ViewModel = new AddCardViewModel(Navigator, Recovery, Session)
            {
                BankName = "Akbank",
                DisplayName = "Axess kredi kartı",
                CreditLimitText = "50000,50",
                StatementDayText = "25",
                PaymentDueDayText = "5",
            };
        }

        public FakeNavigator Navigator { get; } = new();

        public FakeTokenStore Tokens { get; }

        public PendingOperationStore Store { get; }

        public SessionService Session { get; }

        public PendingOperationRecoveryService Recovery { get; }

        public AddCardViewModel ViewModel { get; }

        public void Dispose()
        {
            Store.Dispose();
            _database.Dispose();
        }
    }
}
