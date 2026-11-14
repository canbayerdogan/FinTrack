# ADR-0004 — Tek SQL Server veritabanı, modül başına şema ve DbContext

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Mimari doküman §9

## Bağlam

Modüler monolitte veri sahipliği en kolay aşınan sınırdır. "Sadece bir join atalım"
istisnası, 6 ayda modül sınırlarını yok eder.

Seçenekler:
1. Tek veritabanı, tek şema, tek DbContext — en basit, en kırılgan.
2. Tek veritabanı, **modül başına şema + DbContext** — orta yol.
3. Modül başına ayrı veritabanı — gerçek izolasyon, dağıtık transaction sorunu.

## Karar

**Tek veritabanı (`FinTrackDb`), modül başına şema ve DbContext.**

| Şema | DbContext |
|---|---|
| `expenses` | `ExpensesDbContext` |
| `inflows` | `CashInflowsDbContext` |
| `cards` | `CreditCardsDbContext` |
| `debts` | `DebtsDbContext` |
| `reporting` | `ReportingDbContext` (view/projeksiyon) |
| `identity` | `AppIdentityDbContext` |

Ek kurallar:
- Migration geçmişi şema başına ayrı (`cards.__EFMigrationsHistory`).
- **Modüller arası foreign key yok.** Referanslar `Guid` + Application katmanı doğrulaması.
- Modüller arası join yasak. Raporlama için view veya kendi projeksiyonu.
- Her aggregate `UserId` taşır, global query filter zorunlu.

## Sonuçlar

**Olumlu**
- Veri sahipliği fiziksel olarak görünür (`cards.Transactions` ≠ `expenses.Expenses`).
- Tek yedekleme/geri yükleme, tek connection string.
- Bir modül ileride ayrı DB'ye taşınabilir: şema zaten hazır.
- Cross-module join yazmak "yanlış" hissettirir; sürtünme faydalıdır.

**Olumsuz**
- Modüller arası tutarlılık uygulama seviyesinde (dağıtık transaction yok).
  Örnek: kredi oluşturma → para girişi + taksit planı. **Tek transaction içinde**
  yapılır (aynı DB olduğu için mümkün) ama farklı DbContext'ler → `TransactionScope`
  veya açık `IDbContextTransaction` paylaşımı gerekir.
- Migration sırası yönetilmeli.

## Modüller arası tutarlılık kuralı

Aynı kullanıcı isteğinde birden fazla modül yazacaksa:

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
// modül A yaz
// modül B yaz
await tx.CommitAsync(ct);
```

İki modülü **tek handler içinde** çağırmak yerine, sahibi olan modül
`ModuleContracts` üzerinden diğerini çağırır. Uzun vadede bu akış in-process
olaya dönüşür.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Tek şema, tek DbContext | Sınır korunamaz; en yaygın başarısızlık nedeni. |
| Modül başına ayrı DB | Dağıtık transaction, çoklu connection, yedekleme karmaşası. |
| Ayrı DB + outbox | Bu ölçekte gereksiz; v2'de değerlendirilir. |
