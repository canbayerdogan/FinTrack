using FinTrack.SharedKernel;

namespace FinTrack.Api.Identity;

/// <summary>Sistem saati. Tüm damgalar UTC üretilir.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
