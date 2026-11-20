using FinTrack.Mobile.Core.PendingOperations;

namespace FinTrack.Mobile.Core.Services;

/// <summary>
/// Uygulama başlangıcında, yalnızca oturumu hazır olan kullanıcının
/// SentUnconfirmed kayıtlarını kurtarır (V11-T02 / docs/11 §7.4).
/// Tek uçuş guarantee'si SemaphoreSlim ile sağlanır; hata yutulur, çünkü
/// kurtarma başarısızlığı navigasyonu veya uygulama açılışını bozmamalıdır.
/// Kayıtlar korunur ve sonraki açılışta/yeniden denemede tekrar ele alınır.
/// </summary>
public sealed class PendingOperationStartup
{
    private readonly SessionService _session;
    private readonly PendingOperationRecoveryService _recovery;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PendingOperationStartup(SessionService session, PendingOperationRecoveryService recovery)
    {
        _session = session;
        _recovery = recovery;
    }

    public async Task<RecoveryReport?> RecoverCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        // Aynı anda birden fazla recovery çalışmaz; ikinci çağrı kuyruğa girmek
        // yerine atlanır (aynı kayıtların iki kez gönderilmesi engellenir).
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        try
        {
            var userIdKey = await _session.GetUserIdKeyAsync(cancellationToken);
            if (userIdKey is null)
            {
                return null;
            }

            return await _recovery.RecoverAsync(userIdKey, cancellationToken);
        }
        catch (Exception)
        {
            // Açılış akışı kurtarma hatasına bağlı değildir; mesaj
            // RecoveryService.LastMessage ve kayıtların LastError alanında kalır.
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
