using FinTrack.Mobile.Core.Abstractions;

namespace FinTrack.Mobile.Tests.Fakes;

public sealed class FakeTokenStore : ITokenStore
{
    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public int ClearCount { get; private set; }

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AccessToken);

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(RefreshToken);

    public Task SaveAsync(TokenPair tokens, CancellationToken cancellationToken = default)
    {
        AccessToken = tokens.AccessToken;
        RefreshToken = tokens.RefreshToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        AccessToken = null;
        RefreshToken = null;
        ClearCount++;
        return Task.CompletedTask;
    }
}
