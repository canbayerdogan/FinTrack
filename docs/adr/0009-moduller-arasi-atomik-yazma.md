# ADR-0009 — Modüller arası atomik yazma: sync writer + paylaşılan transaction

**Durum:** Kabul edildi
**Tarih:** 2026-11-17
**İlgili:** ADR-0004, `docs/05` §4.3/§5.5, `docs/06` §6.5, `docs/07` §4 Slice 6 · **[ADR-0010](0010-host-owned-idempotent-writes.md)** (v1.1 genişletmesi)

> **v1.1 notu (2026-11-18):** Bu ADR kabul edilmiş olmaya devam eder. Idempotency
> zorunlu hale geldiğinde transaction'ın sahibi modülden **host middleware**'e
> taşınmıştır; aynı fiziksel bağlantı/aynı `DbTransaction` ilkesi korunur, modül
> sınırları değişmez. Genişletme ve zorunlu koşullar ADR-0010'da tanımlıdır.

## Bağlam

ADR-0004 tek SQL Server veritabanı, modül başına şema/DbContext kararını verdi.
Aynı kullanıcı isteğinde birden fazla modül yazması gerekiyor: nakit avans
(`cards`) + bağlı para girişi (`inflows`), ardından kredi/kişisel borç
(`debts`) + para girişi. İki DbContext bağımsız `SaveChanges` çağrıları yaparsa
yarım kayıt oluşabilir; `docs/06` §6.5 ise "aynı işlemde, yarım kayıt bırakılmaz"
diyor.

Kısıtlar:

- Tek DB, tek connection string; dağıtık transaction yok (ADR-0004).
- Modüller birbirinin tipine/DbContext'ine erişemez; yalnızca `ModuleContracts`.
- Ambient `TransactionScope` SQLite testlerinde desteklenmez; modül testleri
  SQLite in-memory kullanıyor.
- Outbox, okuma anında eksik para girişi anlamına gelir; v1 sözleşmesiyle
  çelişir.

## Karar

**Sahibi modül, `ModuleContracts` içindeki sync writer sözleşmesini çağırır;
her iki modül de aynı `DbConnection` üzerinde açılmış açık bir transaction'a
katılır.**

`ModuleContracts` üç sözleşme taşır:

- `IWriteConnectionProvider` — istek kapsamında tek fiziksel bağlantıyı verir.
- `IWriteTransactionScopeFactory` / `IWriteTransactionScope` — `Serializable`
  transaction açar, `CommitAsync` ve `DisposeAsync` (rollback) sağlar.
- `IDebtCashInflowWriter` — borç sahibi modülün para girişini **aynı
  transaction'da** oluşturmasını/iptal etmesini sağlar.

Kurallar:

1. Transaction'ı **sahibi modülün handler'ı** açar (ör. `CreateCashAdvanceHandler`);
   writer tek başına transaction başlatmaz.
2. Transaction sözleşmesi `System.Data.Common.DbTransaction` kullanır; EF Core
   tipleri `ModuleContracts`'e sızmaz.
3. Yazan her modülün `IUnitOfWork.Enlist(scope)` metodu kendi DbContext'ini
   `Database.UseTransaction(scope.Transaction)` ile bağlar.
4. Writer kendi `SaveChangesAsync` çağrısını yapar; sahibi modül kendi
   `SaveChangesAsync` çağrısını yapar; **commit yalnızca scope üzerinden** olur.
5. Doğrulama hatası, eşzamanlılık hatası veya beklenmeyen istisna durumunda
   scope dispose edilir ve ikinci kayıt da geri alınır.
6. Bağlantının yaşam döngüsü DI scoped servisindedir; scope yalnızca transaction'ı
   kapatır, bağlantıyı kapatmaz.

Akış (özet):

```text
handler → BeginAsync()
        → cards.IUnitOfWork.Enlist(scope)
        → cards kaydını Stage et
        → IDebtCashInflowWriter.CreateAsync(..., scope) → inflow + SaveChanges
        → cards.IUnitOfWork.SaveChangesAsync()
        → scope.CommitAsync()
```

## Sonuçlar

**Olumlu**

- `docs/06` §6.5'teki anlık atomicite korunur: ya iki kayıt da vardır ya hiçbiri.
- Modül sınırı korunur: CreditCards, CashInflows tipi/DbContext'i görmez;
  yalnızca `ModuleContracts` sözleşmesini kullanır.
- Aynı mekanizma Slice 7'de kredi/kişisel borç için de kullanıldı.
- `DbTransaction` tabanlı sözleşme sayesinde modül testleri SQLite'ta gerçek
  rollback davranışını doğrulayabilir.

**Olumsuz / kabul edilen maliyet**

- Yazma yapan modüllerin DbContext kaydı, connection string yerine scoped
  `IWriteConnectionProvider.Connection` kullanacak şekilde değişir.
- Her yazma işleminde bir connection ve bir `Serializable` transaction maliyeti
  vardır; v1'de doğruluk performansa tercih edilir.
- `Enlist` çağrısı unutulursa EF açık transaction'ı görmez; bu yüzden writer
  sözleşmesi `scope` parametresini zorunlu kılar ve testler yarım kaydı arar.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Ambient `TransactionScope` | Microsoft.Data.Sqlite ambient transaction desteklemez; modül testleri kırılır. |
| Host orkestrasyonu | Modül iç akışı host'a sızar; ADR-0004'ün "sahibi modül çağırır" kuralı zayıflar. |
| Outbox / eventual consistency | `docs/06` §6.5'teki "aynı işlemde, yarım kayıt bırakılmaz" sözleşmesiyle çelişir. |
| Tek DbContext / tek şema | ADR-0004 ile çelişir; modül sınırı korunamaz. |

## Ne zaman yeniden değerlendirilir

- Modüllerden biri ayrı veritabanına taşınırsa (dağıtık transaction/outbox
  gerekir).
- Yazma hacmi tek transaction'ı kabul edilemez kılarsa; önce ölçüm, sonra yeni
  ADR.
