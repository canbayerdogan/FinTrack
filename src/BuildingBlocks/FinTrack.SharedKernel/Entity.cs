namespace FinTrack.SharedKernel;

/// <summary>
/// Kimliği olan tüm kalıcı nesnelerin ortak tabanı.
/// Eşitlik kimlik üzerinden tanımlanır; iki farklı aggregate aynı Id'yi taşıyamaz.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other || other.GetType() != GetType())
        {
            return false;
        }

        return Id != Guid.Empty && Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
