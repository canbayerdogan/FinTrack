namespace FinTrack.ModuleContracts;

/// <summary>
/// Modül DbContext'lerinin migration'ını host'un modül iç tiplerini görmeden
/// uygulamasını sağlar. Kayıtlar <c>IEnumerable&lt;IModuleMigration&gt;</c> olarak
/// çözülür; sıra kayıt sırasıdır.
/// </summary>
public interface IModuleMigration
{
    string Module { get; }

    Task MigrateAsync(CancellationToken cancellationToken);
}
