namespace FinTrack.Mobile.Core.Abstractions;

/// <summary>
/// Shell navigasyonu ve kullanıcıya mesaj gösterme işlemlerinin çekirdek
/// katmandan ayrılması; ViewModel testleri platform gerektirmez.
/// </summary>
public interface IAppNavigator
{
    Task GoToAsync(string route, CancellationToken cancellationToken = default);

    Task GoBackAsync(CancellationToken cancellationToken = default);

    Task DisplayAlertAsync(string title, string message, CancellationToken cancellationToken = default);
}
