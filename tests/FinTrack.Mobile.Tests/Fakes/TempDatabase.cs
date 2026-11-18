namespace FinTrack.Mobile.Tests.Fakes;

/// <summary> Her test için benzersiz, geçici SQLite dosyası. </summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"fintrack-pending-{Guid.NewGuid():N}.db");
    }

    public string DatabasePath { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(DatabasePath);
        }
        catch (IOException)
        {
            // Geçici dosya kilitliyse test sonuçlarını etkilemez.
        }
    }
}
