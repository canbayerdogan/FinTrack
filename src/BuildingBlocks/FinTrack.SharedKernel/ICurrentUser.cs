namespace FinTrack.SharedKernel;

/// <summary>
/// İsteği yapan kullanıcı.
/// Değer sunucuda JWT'den okunur; mobil istemcinin gönderdiği bir alana asla güvenilmez.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// Kullanıcının IANA saat dilimi kimliği (JWT <c>preferred_timezone</c> claim'i).
    /// Yerel takvim günü hesaplarında kullanılır; geçersizse varsayılana düşülür.
    /// </summary>
    string PreferredTimeZone { get; }

    /// <summary>Kimlik doğrulanmamışsa <see cref="UnauthorizedAccessException"/> fırlatır.</summary>
    Guid RequireUserId();
}
