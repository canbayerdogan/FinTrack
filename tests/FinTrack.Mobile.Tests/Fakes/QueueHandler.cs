using System.Net;
using System.Net.Http.Json;
using FinTrack.Mobile.Core.Api;

namespace FinTrack.Mobile.Tests.Fakes;

/// <summary>
/// Sırayla tanımlanan yanıtları döndüren ve gelen istekleri kaydeden handler.
/// </summary>
public sealed class QueueHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses;

    public QueueHandler(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses)
    {
        _responses = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(responses);
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary> İstek gövde baytları, istek nesnesi dispose edilmeden yakalanır. </summary>
    public List<byte[]?> Bodies { get; } = [];

    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Json(
        HttpStatusCode status,
        object body) =>
        (_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(body, options: ApiJson.Options),
        });

    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Empty(
        HttpStatusCode status) =>
        (_, _) => Task.FromResult(new HttpResponseMessage(status));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken));
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("Beklenmeyen HTTP isteği: " + request.RequestUri);
        }

        return await _responses.Dequeue()(request, cancellationToken);
    }
}
