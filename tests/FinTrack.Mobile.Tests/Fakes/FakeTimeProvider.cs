namespace FinTrack.Mobile.Tests.Fakes;

/// <summary> Saat bağımlılığını sabitleyen ve ileri sarılabilen TimeProvider. </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; private set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
