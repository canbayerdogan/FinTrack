namespace FinTrack.SharedKernel;

/// <summary>
/// Zaman kaynağı. Testlerde sabitlenebilmesi için doğrudan <c>DateTime.UtcNow</c> kullanılmaz.
/// </summary>
public interface IClock
{
    /// <summary>Her zaman UTC. Veritabanına yazılan tüm damgalar buradan gelir.</summary>
    DateTime UtcNow { get; }
}
