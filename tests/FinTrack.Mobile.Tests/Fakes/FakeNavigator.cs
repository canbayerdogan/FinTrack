using FinTrack.Mobile.Core.Abstractions;

namespace FinTrack.Mobile.Tests.Fakes;

public sealed class FakeNavigator : IAppNavigator
{
    public List<string> Routes { get; } = [];

    public List<string> Alerts { get; } = [];

    public int GoBackCount { get; private set; }

    public Task GoToAsync(string route, CancellationToken cancellationToken = default)
    {
        Routes.Add(route);
        return Task.CompletedTask;
    }

    public Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        GoBackCount++;
        return Task.CompletedTask;
    }

    public Task DisplayAlertAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        Alerts.Add($"{title}: {message}");
        return Task.CompletedTask;
    }
}
