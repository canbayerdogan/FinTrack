namespace FinTrack.ModuleContracts;

/// <summary>
/// Geçici iskelet yanıtı. Modüllerin DI ve yönlendirme zincirine bağlandığını doğrular.
/// Her modülün dilimi tamamlandığında kaldırılacaktır.
/// </summary>
public sealed record ModuleStatus(string Module, string Status = "ready");
