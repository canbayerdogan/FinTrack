# ADR-0010 — Idempotent yazmalarda host sahipliğinde transaction

**Durum:** Kabul edildi (koşullu tasarım onayı 2026-11-18, açık kodlama onayı 2026-11-19) — **uygulamada**
**Tarih:** 2026-11-18 (son güncelleme 2026-11-19)
**İlgili:** [ADR-0009](0009-moduller-arasi-atomik-yazma.md), ADR-0004, [docs/11](../11-idempotency-teknik-tasarim.md) §4, `docs/07` §5.1 (V11-T01)

## Bağlam

ADR-0009, modüller arası atomik yazmayı **sahibi modülün transaction'ı açması**
ve writer'ın aynı `DbTransaction`'a katılmasıyla çözdü. Bu, Slice 6/7 için
yeterliydi.

v1.1'de `Idempotency-Key` zorunlu hale geliyor. İlk tasarımda replay kaydı
(`Completed`) ile finansal kayıt ayrı transaction'larda yazılıyordu:

```text
handler SaveChanges() ── COMMIT ── [ÇÖKME] ── "Completed" yazılamadı
```

Bu pencerede kalıcı bir `InProgress` satırı kalır. 5 dakikalık "stale timeout"
ile otomatik yeniden çalıştırma, finansal kaydı **ikinci kez** oluşturur.
Kullanıcı bu nedenle 5 dk stale timeout'u reddetti ve şu koşulları getirdi:

- `IdempotencyDbContext` ve modül DbContext'leri **aynı fiziksel bağlantıyı ve
  aynı `DbTransaction` nesnesini** kullanmalı.
- Handler `SaveChanges` çağrıları transaction'ı **commit etmemeli**.
- Commit'in **tek sahibi Host middleware** olmalı.
- Commit sonucu belirsizse aynı key ile tekrar deneme güvenli olmalı.
- Commit sırasında bağlantı kopması ayrıca test edilmeli.

Ayrıca ADR-0009 altyapısının tüm mutasyon `POST`'larına genişletilmesi, host
transaction varken modüllerin kendi transaction'ını açmasının engellenmesi, host
transaction yokken mevcut davranışın korunması ve modül sınırlarının değişmemesi
istendi.

## Karar

**Host middleware, kapsam dahili her mutasyon `POST` için tek transaction açar;
idempotency kaydı ve tüm modül yazımları bu transaction'da birlikte commit edilir.
Transaction'ın sahibi host'tur, modül değil.**

Kurallar:

1. `IWriteConnectionProvider` istek kapsamında **tek fiziksel `DbConnection`**
   sağlar. `IdempotencyDbContext` ve ilgili modül DbContext'leri aynı bağlantıyı
   kullanır.
2. Aynı `DbTransaction` nesnesi tüm katmanlara verilir; her DbContext
   `Database.UseTransaction(scope.Transaction)` ile katılır. İkinci transaction
   veya kopya transaction nesnesi yoktur.
3. Modül handler'larının `IUnitOfWork.SaveChangesAsync` çağrıları veri yazar,
   **commit etmez**.
4. `scope.CommitAsync()` yalnızca `IdempotencyMiddleware` tarafından, 2xx yanıt
   tamponlandıktan ve `Completed` kaydı yazıldıktan sonra **bir kez** çağrılır.
   4xx/5xx/istisna → dispose → rollback.
5. **Host transaction varken** modül `IWriteTransactionScopeFactory.BeginAsync()`
   çağrısı yeni transaction açmaz; aynı `Transaction`'ı taşıyan **no-op wrapper**
   döner (`CommitAsync`/`DisposeAsync` etkisiz). Mimari testi, kapsam dahili
   handler'larda `BeginTransactionAsync` olmadığını zorunlu kılar.
6. **Host transaction yokken** (arka plan çağrıları, modül birim testleri,
   migration dışı akışlar) **mevcut ADR-0009 davranışı korunur**: sahibi modül
   kendi transaction'ını açar ve commit eder.
7. Modül sınırları, DbContext sahipliği ve şemalar değişmez. Hiçbir modül
   `IdempotencyDbContext`'e veya `shared.IdempotencyKeys` tablosuna referans
   vermez.
8. `UNIQUE (ScopeHash, Key)` indeksi, commit sonucu belirsiz olsa bile aynı key
   ile tekrar denemeyi güvenli kılar: ya `Completed` replay edilir ya da tek bir
   execution çalışır; **ikinci finansal kayıt fiziksel olarak oluşamaz**.
9. Çoklu API instance için dağıtık kilit yoktur; hakem unique indekstir.
   Commit / rollback / deadlock (1205) / lock timeout (1222) senaryoları ayrı
   test edilir (docs/11 S4, S21–S24).

## Sonuçlar

**Olumlu**

- "Commit oldu ama replay kaydı yazılamadı" penceresi kapanır; kalıcı
  `InProgress` satırı oluşmaz, stale-lock kurtarması gerekmez.
- Belirsiz bağlantı kopmasında aynı key ile tekrar denemek güvenlidir.
- Replay kaydı ile finansal kayıt her zaman tutarlıdır (ya ikisi de var ya
  hiçbiri).
- ADR-0009'un modül sınırı koruma ilkesi korunur; yalnızca transaction sahibi
  değişir.

**Olumsuz / kabul edilen maliyet**

- Transaction süresi middleware'in yanıt tamponuna kadar uzar; handler içinde
  dış I/O yasaktır, command timeout yapılandırılır.
- Modül handler'ları `Enlist`/no-op uyumu gerektirir (Expenses dâhil); iş mantığı
  değişmez ama altyapı kodu dokunur.
- İki instance eşzamanlılığında kısa bloklama olabilir; lock timeout durumunda
  `409 IdempotencyInProgress` döner.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| 5 dk stale `InProgress` timeout | Kullanıcı reddetti: commit sonrası çökmede çift kayıt riski |
| Replay kaydını ayrı transaction'da yazmak | Aynı atomiklik açığı; çökme penceresi kalır |
| Dağıtık kilit / Redis | Yeni altyapı; unique index yeterli, maliyet gereksiz |
| Ambient `TransactionScope` | Microsoft.Data.Sqlite desteklemez (ADR-0009 gerekçesi) |
| Host orkestrasyonunun iş mantığını üstlenmesi | Modül sınırı bozulur; yalnızca transaction sahipliği host'a geçer |

## Test zorunlulukları

- S21: `CommitAsync` sırasında bağlantı kopması → aynı key retry → 1 kayıt.
- S18: ADR-0009 cross-module atomiklik idempotency altında.
- S25: Expenses, CashInflows, CreditCards, Debts için host scope entegrasyonu.
- S4/S22/S23/S24: çoklu instance ve commit/rollback/deadlock/lock timeout matrisi.
- Mimari testi: kapsam dahili handler'da ikinci transaction açılmaz; modül
  `IdempotencyDbContext`'e referans vermez.

## Ne zaman yeniden değerlendirilir

- İşlemler ayrı veritabanlarına taşınırsa (dağıtık transaction/outbox gerekir).
- Ölçümler transaction süresinin kabul edilemez olduğunu gösterirse; önce veri,
  sonra yeni ADR.
- Sağlayıcı `Microsoft.Data.Sqlite` yerine ambient transaction destekleyen bir
  test altyapısına geçerse (no-op wrapper sadeleşebilir).
