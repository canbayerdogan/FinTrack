# FinTrack — Transaction Participant Architecture Audit

> **Kapsam notu:** Bu rapor yalnızca kaynak kodu ve mevcut test dosyalarının incelenmesine dayanır. **Hiçbir test çalıştırılmadı**, hiçbir dosya değiştirilmedi/oluşturulmadı/silinmedi, refactoring yapılmadı, commit/push yapılmadı. Çalışma ağacı denetim boyunca temiz kaldı (`main...origin/main`). API integration testleri `Testcontainers.MsSql` + Docker gerektirir (`tests/FinTrack.Api.IntegrationTests/ApiFixture.cs:4,16-28`); bu denetimde çalıştırılmadı. "Test kanıtı" ifadeleri, test kaynak kodundaki assert'leri anlatır; çalıştırılmış sonuç anlamına gelmez. Çalışma zamanına dair framework davranışları (ADO.NET rollback semantics, EF `UseTransaction` uyumluluğu) kodda kanıtlanamadığı için açıkça "doğrulanmamış" olarak işaretlenmiştir.
>
> **Ekler hakkında:** Bu not yalnızca ilk denetim anına aittir. Sonradan yapılan
> test/production değişiklikleri ve çalıştırılmış test sonuçları §9 (V12-T01),
> §9.6 (F-06/F-07/F-08) ve **§10 (V12-T02)** bölümlerinde kaydedilmiştir.
> Bulguların güncel durumu için §10.3 esas alınmalıdır.

---

## 1. Executive Summary

1. **Önceki Y-3 tespiti bağımsız olarak doğrulandı, ancak yalnızca *yapısal* fark olarak:** Expenses/CreditCards `UnitOfWork` sınıflarında modül-yerel `ITransactionScopeFactory` + `HostTransactionScope` no-op dalı varken (`Expenses/.../UnitOfWork.cs:51-88`, `CreditCards/.../UnitOfWork.cs:51-88`), CashInflows/Debts yalnızca `Enlist`/`SaveChanges` içerir, yerel fabrika yoktur (`CashInflows/.../UnitOfWork.cs:9-48`, `Debts/.../UnitOfWork.cs:9-48`). Bu farkın **atomiklik açığına yol açtığı kanıtlanmadı**.
2. **Dört modül de aynı `IWriteScopeParticipant` sözleşmesini uygular** ve `Enlist` gövdeleri birebir aynıdır (referans eşitliği guard'ı + `Database.UseTransaction`) — `AtomicWrites.cs:46-49`; `Expenses UnitOfWork.cs:16-28`, `CreditCards:16-28`, `CashInflows:15-27`, `Debts:15-27`. Mimari test bunu zorunlu kılar (`AtomicWriteBoundaryTests.cs:89-108`).
3. **Host yolu gerçekten merkezi:** `IdempotencyMiddleware` tek `Serializable` transaction açar, idempotency DbContext + dört participant'ı aynı `DbTransaction`'a bağlar, handler'ları `next(context)` içinde çalıştırır (`IdempotencyMiddleware.cs:141-155,207`). Gerçek commit yalnızca `:259` (2xx) ve `:345` (expired→tombstone) noktalarında yapılır.
4. **Handler seviyesindeki commit'ler host yolunda no-op'tur:** host `NoOpWriteTransactionScope.CommitAsync` boş gövdelidir (`SqlWriteTransactions.cs:80-87`), modül-yerel `HostTransactionScope` da öyle (`Expenses/CreditCards UnitOfWork.cs:74-79`). Yani "commit kime ait?" sorusunun cevabı yola göre değişir: **in-scope POST'larda Host, host-scope olmayan fallback'de modül handler'ı**.
5. **Atomiklik kanıtı kısmi ama gerçek:** S18 gerçek MSSQL'de nakit avans + bağlı para girişinin host commit eşiğinde birlikte rollback olduğunu gösterir (`IdempotencyCoreScenariosTests.cs:268-299`); modül testleri gerçek SQLite'ta iki DbContext arası rollback gösterir (`CashAdvanceAtomicityTests.cs:56-84`, `AtomicWriteTests.cs:62-86`). Ancak hiçbir test **ikinci participant'ın gerçek DB yazısından sonra hata vermesini** veya **gerçek `DbTransaction.CommitAsync` istisnasını** tetiklemez.
6. **S25 yanıltıcıdır:** Adı "dört modül aynı transaction'da" dese de gövdesi dört **ayrı** POST ve dört **ayrı** `Guid.NewGuid()` anahtarıdır (`IdempotencyCoreScenariosTests.cs:301-342`). Dört modülün tek transaction'a katıldığını **kanıtlamaz**.
7. **Rollback açıkça çağrılmaz:** host yazma yolunda `RollbackAsync` yoktur; geri alma, commit edilmemiş `DbTransaction.DisposeAsync()` davranışına dayanır (`SqlWriteTransactions.cs:71-72`, middleware `:285-315`). Bu framework davranışı kaynak kodda kanıtlanamaz; yalnızca yorumda ifade edilir.
8. **Küçük kod kokuları:** `committed` yerel değişkeni atanır ama hiç okunmaz (`IdempotencyMiddleware.cs:143,160,198,260`); ADR-0010 "tek commit" derken middleware'de iki commit noktası vardır (`:259`, `:345`); `CashAdvanceHandlers.cs:12` yorumu ADR-0010 öncesi "commit yalnızca handler'da" ifadesini taşır.
9. **Koruma altında olmayan yollar var:** PUT istekleri ve auth POST'ları middleware kapsamı dışındadır (`IdempotencyMiddleware.cs:378-392`); kimlik doğrulama başarısızlığında `:76-80` host scope açılmadan `next` çağrılır. Bu yollarda yazma olabileceğine dair test yok.
10. **Öneri yönü:** Mevcut host-merkezi model (Seçenek C) esasen uygulanmıştır; Y-3 farkı host yolunda etkisizleştirilmiş bir **ADR-0009 kalıntısıdır**. Riski düşürmek için B (radikal birleştirme) değil, **C'yi pekiştiren küçük, test odaklı adımlar** önerilir: zorlayıcı mimari testler, ölü kod temizliği ve aynı transaction/connection kimliğini doğrulayan testler. Kesin karar bölüm 6'da tüm seçeneklerle birlikte verilmiştir.

---

## 2. Mevcut Mimari

### 2.1 Ortak sözleşmeler ve DI

`src/BuildingBlocks/FinTrack.ModuleContracts/AtomicWrites.cs` sözleşme seti:

| Satır | Sözleşme | Rol |
|---|---|---|
| 10-13 | `IWriteConnectionProvider.Connection` | İstek başına tek fiziksel `DbConnection` |
| 19-24 | `IWriteTransactionScope` (`Transaction`, `CommitAsync`, `IAsyncDisposable`) | Açık transaction sınırı; EF tipleri sözleşmeye sızmaz |
| 26-29 | `IWriteTransactionScopeFactory.BeginAsync` | Transaction başlatma |
| 36-39 | `IWriteTransactionScopeAccessor.Current` | Modüllere salt-okunur host scope (null = host transaction yok) |
| 46-49 | `IWriteScopeParticipant.Enlist(IWriteTransactionScope?)` | Participant kaydı |
| 88-100 | `IDebtCashInflowWriter.CreateAsync/CancelAsync` | Writer kendi transaction'ını açmaz/commit etmez; scope'u parametre alır |

DI (`src/Host/FinTrack.Api/Program.cs:59-72`): `IWriteConnectionProvider` → scoped `SqlWriteConnectionProvider` (`:60`), `IWriteTransactionScopeFactory` → scoped `SqlWriteTransactionScopeFactory` (`:61`), setter/accessor **aynı scoped örnek** (`:64-66`), `IdempotencyDbContext` aynı `IWriteConnectionProvider.Connection` üzerinde (`:69-72`). Dört modül kaydı `:140-146`'da, middleware pipeline `:170-176`'da (`UseExceptionHandler` middleware'in dışında).

Tüm modül DbContext'leri aynı scoped bağlantıyı kullanır: `ExpensesInfrastructureRegistration.cs:16-19`, `CreditCardsInfrastructureRegistration.cs:16-19`, `CashInflowsInfrastructureRegistration.cs:16-19`, `DebtsInfrastructureRegistration.cs:16-19`.

### 2.2 Host transaction yaşam döngüsü (ADR-0010 yolu)

`IdempotencyMiddleware.InvokeAsync`:

- **Kapsam tespiti:** `InScopePrefixes` = expenses, categories, cash-inflows, cards, personal-debts, loans (`IdempotencyMiddleware.cs:21-29`); `IsInScope` yalnızca POST + prefix, `/api/v1/auth` hariç (`:378-392`). Kapsam dışı → `await next(context); return;` (`:45-49`).
- **Açılış:** `participants.ToArray()` (`:141`), `scope = await transactionFactory.BeginAsync(...)` (`:149`), `scopeSetter.Set(scope)` (`:150`), `idempotencyDb.Database.UseTransaction(scope.Transaction)` (`:151`), `foreach participant.Enlist(scope)` (`:152-155`).
- **Anahtar kaydı** aynı transaction içinde `InProgress` olarak yapılır (`:157-201`).
- **Handler çalıştırma:** response buffer kurulur, `await next(context)` (`:203-212`).
- **Commit:** yalnızca `statusCode is >=200 and <=299` için `store.CompleteAsync` (`:237-246`) ardından `await scope.CommitAsync(...)` (`:259`). Test hook'ları `:247-257` (BeforeCommit/DuringCommit → commit öncesi throw) ve `:262-266` (AfterCommit → commit sonrası throw) araya girer; catch `:268-274` durumu "belirsiz" sayıp 409 `IdempotencyInProgress` döner.
- **Rollback/temizlik (finally):** `scopeSetter.Set(null)` (`:287`) → `idempotencyDb.Database.UseTransaction(null)` (`:288`) → `participant.Enlist(null)` döngüsü, participant başına try/catch (`:289-299`) → `await scope.DisposeAsync()` (`:305`) → `buffer?.Dispose()` (`:313`) → `_activeKeys.TryRemove` (`:314`). Sipariş kritiktir: setter önce temizlenir, böylece `Enlist(null)` accessor fallback'i `null` görür ve DbContext'ten transaction'ı ayırır.
- **İkinci commit noktası:** `HandleExistingAsync` expired completed kayıtta `TombstoneAsync` + `scope.CommitAsync` (`:341-348`).

`SqlWriteTransactions.cs`: `SqlWriteTransactionScopeFactory.BeginAsync` host scope varsa `NoOpWriteTransactionScope` döner (`:46-49`), yoksa shared connection'ı açıp `IsolationLevel.Serializable` transaction başlatır (`:51-61`). Gerçek scope `CommitAsync`'i doğrudan `DbTransaction.CommitAsync`'e delege eder (`:65-73`); no-op scope aynı `DbTransaction`'ı sunar, commit/dispose boş gövdelidir (`:80-87`).

### 2.3 Yaklaşım 1 — Expenses / CreditCards (Y-3'ün "farklı" tarafı)

- **Ortak `Enlist`:** `Expenses/.../UnitOfWork.cs:16-28`, `CreditCards/.../UnitOfWork.cs:16-28` — dört modülle aynı desen.
- **Ek modül-yerel fabrika:** `TransactionScopeFactory.BeginSerializableAsync` host scope varsa `HostTransactionScope` (no-op) döner (`:55-64`), yoksa `db.Database.BeginTransactionAsync(IsolationLevel.Serializable)` ile gerçek EF transaction (`:66-70`, `:81-88`). `HostTransactionScope.CommitAsync`/`DisposeAsync` no-op (`:74-79`).
- **Kullanan handler'lar:** `ExpenseHandlers.cs:95,181` (commit `:163,216`), `RefundHandlers.cs:25,75` (commit `:58,116`), `StatementHandlers.cs:57,82`. Bu handler'lar `IWriteScopeParticipant`'ı doğrudan kullanmaz; koruma modül-yerel no-op dalındadır.
- **Cross-module akış:** `CashAdvanceHandlers.cs:57-107` — shared `IWriteTransactionScopeFactory.BeginAsync` (`:57`), `unitOfWork.Enlist(scope)` (`:61`), writer'a scope geçişi (`:64-76`), owner `SaveChangesAsync` (`:88`), `scope.CommitAsync` (`:99`) — host yolunda no-op, `finally` detach + dispose (`:103-107`). İptal simetrik (`:151-197`).
- **"Expense with card" yazma değildir:** `ExpenseReferenceService.cs:13-17,40-73` yalnızca `ICardQuery` ile aktif kart doğrular; kart yazmaz. Kart ödemesi de tek modüldür (`CardPaymentHandlers.cs:8-10`).

### 2.4 Yaklaşım 2 — CashInflows / Debts (Y-3'ün "sade" tarafı)

- **Ortak `Enlist`:** `CashInflows/.../UnitOfWork.cs:15-27`, `Debts/.../UnitOfWork.cs:15-27` — aynı gövde; **yerel fabrika yok**.
- **Cross-module akışlar shared fabrikayı kullanır:** `LoanHandlers.cs:46-95`, `PersonalDebtHandlers.cs:45-94`, `CashAdvanceHandlers.cs:57-107` — `IWriteTransactionScopeFactory.BeginAsync` + `Enlist(scope)` + writer + `SaveChanges` + `CommitAsync` (no-op).
- **Writer:** `DebtCashInflowWriter.cs:18-66 / 68-117` — `Enlist(scope)` (`:47,:81`), kendi `SaveChangesAsync` (`:52,:105`), `finally Enlist(null)` (`:64,:115`), kendi transaction açmaz (`:9-13` yorumu).
- **Domain korumaları:** `CashInflow.cs:169-172` (bağlı inflow update reddi), `:198-201` (bağlı inflow cancel reddi), `:219-235` (`CancelFromDebtOwner`); bağlantı `DebtLinkId` `:150`'de kurulur.
- **Tek modüllü akışlar** (`CashInflowHandlers.cs:43,101,151`; `LoanHandlers.cs:228,292`; `PersonalDebtHandlers.cs:214,271`) yalnızca kendi `SaveChangesAsync`'ini çağırır, fabrika/commit kullanmaz.

### 2.5 Y-3 yargısı

Y-3'ün *yapısal* iddiası **doğrudur**: Expenses/CreditCards'ta ek bir modül-yerel `ITransactionScopeFactory` ve `HostTransactionScope` no-op dalı varken CashInflows/Debts'ta bu yoktur. Ancak bu farkın çalışma zamanı atomiklik farkı oluşturduğu **kanıtlanmamıştır**: host scope aktifken (a) dört modül de aynı `DbTransaction`'a enlist edilir, (b) her iki taraftaki handler commit'i no-op wrapper'dan geçer (sırasıyla `HostTransactionScope` ve `NoOpWriteTransactionScope`), (c) gerçek commit host'tadır. Fark, etkisizleştirilmiş bir **ADR-0009 mirası** niteliğindedir.

---

## 3. Karşılaştırma Tablosu

| Başlık | Expenses / CreditCards | CashInflows / Debts |
|---|---|---|
| **Transaction başlatma** | Host yolu: `IdempotencyMiddleware.cs:149` → `SqlWriteTransactionScopeFactory.BeginAsync` (`:43-62`, `Serializable`). Ek olarak modül-yerel `TransactionScopeFactory.BeginSerializableAsync` (`Expenses/CreditCards UnitOfWork.cs:51-70`), host scope varsa `HostTransactionScope` no-op (`:55-64`). | Host yolu: aynı middleware/fabrika (`:149`). Yerel fabrika **yok**; cross-module handler'lar shared `IWriteTransactionScopeFactory.BeginAsync`'i çağırır (`LoanHandlers.cs:46`, `PersonalDebtHandlers.cs:49`, `CashAdvanceHandlers.cs:57`). |
| **Participant kaydı** | `Enlist` (`UnitOfWork.cs:16-28`): `scope ?? accessor.Current` → `ReferenceEquals` guard → `UseTransaction`. Cross-module cash advance'de handler da `Enlist(scope)` çağırır (`CashAdvanceHandlers.cs:61,155`). Diğer handler'lar participant kaydını middleware'e bırakır. | `Enlist` birebir aynı gövde (`CashInflows/Debts UnitOfWork.cs:15-27`). Writer `Enlist(scope)` (`:47,81`) + `finally Enlist(null)` (`:64,115`) yapar; owner handler da enlist eder. |
| **Commit yönetimi** | Host yolu: handler `CommitAsync` no-op (`HostTransactionScope:74-79` veya `NoOpWriteTransactionScope:80-87`), gerçek commit middleware `:259/:345`. Fallback: `EfTransactionScope.CommitAsync` gerçek EF commit (`UnitOfWork.cs:81-87`). | Host yolu: aynı no-op mekanizması (`NoOpWriteTransactionScope`), gerçek commit middleware'de. Fallback: `WriteTransactionScope.CommitAsync` → `DbTransaction.CommitAsync` (`SqlWriteTransactions.cs:65-73`). |
| **Rollback yönetimi** | Host: commit edilmemiş scope'un `DisposeAsync()` (middleware `:305` → `SqlWriteTransactions.cs:72`). Modül-yerel fallback: `HostTransactionScope.DisposeAsync` no-op veya `EfTransactionScope.DisposeAsync` (`:81-88`). Açık `RollbackAsync` yok. | Aynı host rollback mekanizması; handler `finally` bloklarında `scope.DisposeAsync()` (`LoanHandlers.cs:93-94`, `PersonalDebtHandlers.cs:92-93`) host yolunda no-op'tur. Açık `RollbackAsync` yok. |
| **Hata davranışı** | `SaveChangesOutcome` → `Result.Failure` → 4xx → middleware commit etmez → rollback. Writer hatası `CashAdvanceHandlers.cs:77-80`'de erken dönüş. Commit-area hook exception → 409 `:268-274`. | Aynı desen: `LoanHandlers.cs:66-69`, `PersonalDebtHandlers.cs:65-68` writer hatasında erken dönüş; 4xx → rollback. UoW SQL 2601/2627 → `DuplicateKey`, concurrency → `ConcurrencyConflict` (`UnitOfWork.cs:29-44`). |
| **Modüller arası koordinasyon** | Tek gerçek cross-module yazma: nakit avans (cards) + inflow. Kart/gider etkileşimi salt-okunur. Tüm koordinasyon host transaction'ı + `IDebtCashInflowWriter` contract'ı ile. | Üç cross-module yazma: loan + inflow, personal debt + inflow, cash advance (cards) + inflow — hepsi aynı writer + scope deseni. Repayment/cancel akışları tek modüldür. |
| **Test kapsamı** | S18 gerçek MSSQL atomiklik (`IdempotencyCoreScenariosTests.cs:268-299`); `CashAdvanceAtomicityTests.cs:25-122` gerçek SQLite iki-DbContext rollback; `CashAdvanceHandlerTests.cs:23-61,257-276` fake scope kimliği/commit; `AtomicWriteBoundaryTests.cs:110-135` **kaynak-metin** no-op kontrolü (yalnızca bu iki modül). | `AtomicWriteTests.cs:35-146` gerçek SQLite loan/debt + inflow rollback; `LoanHandlerTests.cs:96-112`, `PersonalDebtHandlerTests.cs:82-97` fake scope kimliği. Bu owner'lar için **gerçek MSSQL participant-hata testi yok**; S25 zayıf genel kapsam sağlar (bölüm 5). |

---

## 4. Veri Bütünlüğü Analizi

### 4.1 İlk modülün yazması başarılı, ikinci modülün yazması başarısız

- **Mevcut davranış (kod):** İkinci taraf `SaveChangesOutcome` failure döndürürse owner handler `Result.Failure` ile döner (`CashAdvanceHandlers.cs:77-80`, `LoanHandlers.cs:66-69`, `PersonalDebtHandlers.cs:65-68`), endpoint 4xx üretir, middleware'in 2xx bloğu (`:225`) atlanır, `finally` `:305` scope'u dispose eder → ilk modülün staged yazısı da geri alınır. Beklenmeyen exception durumunda `:280-284` rethrow + `:285-315` temizlik.
- **Kaynak kod kanıtı:** `IdempotencyMiddleware.cs:225-275,280-315`; `SqlWriteTransactions.cs:65-73`; `CashAdvanceHandlers.cs:59-107`; `DebtCashInflowWriter.cs:18-66`.
- **Test kanıtı:** `CashAdvanceAtomicityTests.cs:56-84` ve `AtomicWriteTests.cs:62-86` — **gerçek SQLite**, iki DbContext, owner UoW'su zorla `ConcurrencyConflict` döndürülüyor (writer gerçekten yazdıktan sonra), taze context'lerde 0 satır. S18 (`IdempotencyCoreScenariosTests.cs:268-299`) gerçek MSSQL'de commit eşiğinde 0 + 0 satır doğruluyor.
- **Kanıtlanmayan:** Hiçbir test **ikinci participant'ın (writer'ın) gerçek DB yazısından sonra** hata vermesini tetiklemez; `FakeDebtCashInflowWriter.NextCreateFailure` yalnızca `CashAdvanceHandlerTests.cs:264`'te kullanılır ve fake hiç DB'ye yazmaz. `NextCancelFailure` hiçbir yerde kullanılmaz. Hata enjeksiyonu ya host hook'u ya da owner UoW'dur.
- **Risk seviyesi:** **Düşük–Orta.** Kod yolu ve iki gerçek DB testi rollback'i destekliyor; ancak "gerçek ikinci participant hatası" senaryosu kanıtlanmadı.

### 4.2 Participant kaydı sırasında exception

- **Mevcut davranış (kod):** `Enlist(scope)` döngüsü middleware'de **try/catch olmadan** çağrılır (`:152-155`); yalnızca detach döngüsü (`:289-299`) try/catch'lidir. `Enlist` içindeki `db.Database.UseTransaction(transaction)` (`UnitOfWork.cs:25`) istisna verirse try bloğu `:147` terkedilir, `finally :285-315` devreye girer, scope dispose edilir → veri yazılmadığı için atomiklik korunur; ancak istek 500 olur ve `InProgress` idem kaydı da rollback olur (retry açık kalır).
- **Kaynak kod kanıtı:** `IdempotencyMiddleware.cs:147-155,285-315`; `UnitOfWork.cs:15-27` (dört modül).
- **Test kanıtı:** **Yok.** `AtomicWriteBoundaryTests.cs:89-108` yalnızca tip/interface varlığını doğrular; `Enlist` çalıştırılmaz.
- **Risk seviyesi:** **Orta.** Veri bütünlüğü koruma altında görünse de davranış test edilmedi ve hata yönetimi (graceful 409 vs 500) belirsiz.

### 4.3 Commit sırasında exception

- **Mevcut davranış (kod):** `scope.CommitAsync` (`:259`) doğrudan `DbTransaction.CommitAsync`'e delege eder (`SqlWriteTransactions.cs:69`), try/catch yok. Middleware catch'i (`:268-274`) herhangi bir commit-area hatasını "sonuç belirsiz" sayıp 409 `IdempotencyInProgress` döner. Gerçek commit başarılı olup da sonrasında (AfterCommit hook) hata olursa 409 + commit'li veri bir arada kalabilir (S6).
- **Kaynak kod kanıtı:** `IdempotencyMiddleware.cs:247-274`; `SqlWriteTransactions.cs:65-73`; `IdempotencyTestHooks.cs:33-39`.
- **Test kanıtı:** S5 (`IdempotencyFailureAndLifecycleTests.cs:17-46`) commit **öncesi** hook → 0 finans + 0 idem satırı; S6 (`:48-79`) commit **sonrası** hook → 409 ama 1 satır + `Completed`; S21 (`:81-110`) "DuringCommit" → 409, retry 1 satır. **Kritik not:** `DuringCommit` hook'u `:252-257`'de commit çağrısından **önce** throw eder; yani **gerçek `DbTransaction.CommitAsync` istisnası hiçbir testte tetiklenmez**.
- **Risk seviyesi:** **Orta.** Belirsiz-sonuç stratejisi test edilmiş; gerçek commit istisnası ve `WriteTransactionScope.CommitAsync` hata davranışı test edilmemiştir.

### 4.4 Aynı transaction içinde aynı participant'ın tekrar kaydedilmesi

- **Mevcut davranış (kod):** Dört `Enlist` gövdesinde `ReferenceEquals(_enlisted, transaction)` guard'ı (`UnitOfWork.cs:20-23`) aynı `DbTransaction` referansı için `UseTransaction`'ı atlar. Middleware `:152-155` de-duplication yapmaz; handler `Enlist(scope)` + writer `Enlist(scope)` + `finally Enlist(null)` çağrıları aynı transaction referansına düşer → guard devreye girer. Accessor temizlenmeden önceki `Enlist(null)` host transaction'ına fallback eder (`:19`), `:287`'den sonraki `Enlist(null)` ise `UseTransaction(null)` ile ayırır.
- **Kaynak kod kanıtı:** `UnitOfWork.cs:15-27` (dört modül); `IdempotencyMiddleware.cs:141-155,287-299`; `DebtCashInflowWriter.cs:47,64,81,115`.
- **Test kanıtı:** **Yok.** `CountingUnitOfWork.Enlist` sadece içeri delege eder; çift enlist senaryosu yok, guard test edilmez.
- **Risk seviyesi:** **Düşük.** Kod guard'ı açık ve deterministik; ancak regresyona karşı test yok.

### 4.5 Transaction tamamlanmadan uygulama hatası oluşması

- **Mevcut davranış (kod):** Handler içinde yakalanmayan exception → middleware `:280-284` response body'yi geri kurup rethrow → `finally :285-315` participant detach + scope dispose → dışarıdaki `UseExceptionHandler` (`Program.cs:170`) 500 yazar. Commit edilmediği için tüm enlisted yazılar ve idem kaydı geri alınır. Yakalanan `SaveChangesOutcome` failure'ları 4xx → aynı rollback yolu.
- **Kaynak kod kanıtı:** `IdempotencyMiddleware.cs:280-315`; `Program.cs:170-176`; handler `try/finally` blokları (`CashAdvanceHandlers.cs:59-107`, `LoanHandlers.cs:46-95`).
- **Test kanıtı:** Dolaylı — S5/S18/S22 hook tabanlı commit-öncesi hatalar; modül testlerindeki zorla `ConcurrencyConflict` (exception değil, failure Result). **Handler içinde, bir modül yazdıktan sonra fırlatılan gerçek exception** test edilmez.
- **Risk seviyesi:** **Düşük–Orta.** Yapısal olarak rollback doğru; gerçek exception senaryosu için doğrudan kanıt yok.

### 4.6 Bir modülün koordinasyonu atlayıp kendi başına kaydetmesi

- **Mevcut davranış (kod):** Host scope varken tüm handler commit'leri no-op wrapper'dan geçer (`SqlWriteTransactions.cs:80-87`, `HostTransactionScope:74-79`); bu nedenle "kendi başına commit" etkisizdir. Middleware kapsamı dışındaki yollar: **PUT** (`IsInScope` POST-only, `:378-392`) → `SaveChangesAsync` host transaction'sız (`CashInflowHandlers.cs:101`, `ExpenseHandlers.cs:152`, `CardEndpoints.cs:77` vb.); **auth POST** (`:386-389` hariç, kendi transaction'ı `AuthEndpoints.cs:84-110`); **kimlik doğrulama hatası yolu** (`:76-80` host scope açmadan `next`); **cleanup** ayrı scope/connection (`IdempotencyCleanupService.cs:46-88`, middleware `:131-132`); **modül testleri/direkt handler çağrı** (accessor `null` → gerçek fallback transaction).
- **Kaynak kod kanıtı:** yukarıdaki dosya/satırlar; `Program.cs:64-66` accessor'ın modüllere salt-okunur verilmesi.
- **Test kanıtı:** `WriteTransactionInternalsTests.cs:44-83` no-op wrapper'ı fake ile doğrular; `AtomicWriteBoundaryTests.cs:110-135` yalnızca Expenses/CreditCards için **kaynak-metin** kontrolü yapar. **Host scope altında bir handler'ın kendi commit'inin gerçekten no-op olduğunu gözlemleyen davranışsal test yok**; `:76-80` yolunda yazma olabileceğine dair test yok.
- **Risk seviyesi:** **Orta.** Mevcut kodda bypass etkisiz görünse de, bunu zorunlu kılan bir mimari test yok; gelecekteki endpoint'ler için koruma zayıf.

### 4.7 Farklı DbContext / repository'lerin aynı veritabanı transaction'ına gerçekten katılıp katılmadığı

- **Mevcut davranış (kod):** Tüm modül DbContext'leri + `IdempotencyDbContext` aynı scoped `IWriteConnectionProvider.Connection`'ı kullanır (`Program.cs:60,69-72`; modül registration `:16-19`). Middleware aynı `scope.Transaction`'ı idempotency DbContext'e `UseTransaction` (`:151`) ve her participant'a `Enlist` (`:152-155`) ile bağlar. EF Core tarafında `Database.UseTransaction(DbTransaction)` çağrısı, verilen transaction'ın aynı `DbConnection`'a ait olmasını gerektirir; kod bu bağı paylaşır.
- **Kaynak kod kanıtı:** yukarıdaki DI + middleware satırları; dört `UnitOfWork.Enlist`.
- **Test kanıtı:** **Güçlü ama sınırlı.** `CardTestDatabase.cs:29-35,53-62,70-75` ve `DebtTestDatabase.cs:29-35,53-62,70-75` gerçek tek SQLite connection'ı açar, iki şemayı orada oluşturur; `WriteTransactionTestDoubles.cs:23-28` bu connection'da gerçek `DbTransaction` başlatır; `CashAdvanceAtomicityTests.cs:56-84` ve `AtomicWriteTests.cs:62-86` taze context'lerle rollback'i doğrular; S18 gerçek MSSQL'de 0 + 0 satır görür.
- **Kanıtlanmayan:** Hiçbir test `Assert.Same(db.Database.GetDbConnection(), ...)`, `Database.CurrentTransaction == scope.Transaction` veya `GetEnlistedTransaction` gibi **açık kimlik assert'i** yapmaz. "Aynı connection/transaction" bir fixture **kurulum gerçeğidir**, test edilen bir değişmez değildir. 3+ modülün tek transaction'ı kanıtlanmaz (S25 bkz. bölüm 5).
- **Risk seviyesi:** **Düşük–Orta.** İki modül için gerçek DB kanıtı var; kimlik invariant'ı ve 3+ modül kanıtlanmadı.

---

## 5. Test Coverage Gap

### 5.1 İlgili test envanteri ve neyi kanıtladıkları

| Test dosyası | Kapsam | Gerçek DB mi? |
|---|---|---|
| `tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs:21-110` | accessor Set/Set(null) (`:21-42`), no-op wrapper'ın aynı `DbTransaction`'ı paylaşıp commit/dispose etmediği (`:44-58`), host scope varken fabrikanın connection açmadan no-op döndüğü (`:60-83`), idem hata sınıflandırması (`:85-110`) | **Fake** (`FakeDbTransaction:142-179`); gerçek DB/transaction yok |
| `tests/FinTrack.Api.UnitTests/IdempotencyMiddlewareScopeTests.cs:19-41` | `IsInScope` prefix/kuraları | Reflection/structural |
| `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs:48-135` | modül→host bağımlılığı yok (`:48-67`), Application→EF/SqlClient yok (`:69-87`), dört UoW `IWriteScopeParticipant` (`:89-108`), Expenses/CreditCards **kaynak-metin** no-op dalı (`:110-135`) | Kaynak metin / metadata |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs:25-122` | iki DbContext成功 yazımı (`:25-54`), owner hata → iki tabloda 0 satır (`:56-84`), iptal rollback (`:86-122`) | **Gerçek SQLite**, tek connection |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceHandlerTests.cs:23-61,90-127,257-276` | scope kimliği (`Assert.Same`, `:50,:120`), commit=1 (`:51`), writer hatasında commit=0 (`:257-276`) | Fake writer/scope factory |
| `tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs:35-146` | loan/debt + inflow başarı ve owner hata rollback'i (`:62-86,88-122`) | **Gerçek SQLite** |
| `tests/FinTrack.Modules.Debts.Tests/Application/LoanHandlerTests.cs:96-112`, `PersonalDebtHandlerTests.cs:82-97` | fake scope kimliği (`:111`, `:96`) | Fake writer |
| `tests/FinTrack.Api.IntegrationTests/IdempotencyCoreScenariosTests.cs:268-299` (S18) | gerçek MSSQL: DuringCommit hook → 0 + 0 satır, retry 201 + 1+1 | **Gerçek SQL Server 2022 (Docker)** |
| `...IdempotencyCoreScenariosTests.cs:301-342` (S25) | dört ayrı POST, dört ayrı key, hepsi 201, 1+1 satır | Gerçek MSSQL, **ama tek transaction değil** |
| `...IdempotencyFailureAndLifecycleTests.cs:17-110` (S5/S6/S21) | commit öncesi/sonrası belirsizlik ve retry | Gerçek MSSQL |
| `...IdempotencyMultiInstanceTests.cs:15-138` (S4/S22/S23/S24) | iki instance, rollback→retry, lock timeout | Gerçek MSSQL |
| `tests/FinTrack.Api.IntegrationTests/ApiFixture.cs:4,16-28`; `ApiCollection.cs:6-9`; `.csproj:24` | Testcontainers.MsSql 4.16.0, 11 sınıf Docker'a bağımlı; `SmokeTests.cs:11-42` Docker'sız | Docker **zorunlu**, skip yok |

### 5.2 Kapsanmayan / yetersiz senaryolar

1. **Enlist istisnası** — `Enlist(scope)` döngüsü middleware'de korunmasız (`:152-155`); test yok.
2. **Gerçek `DbTransaction.CommitAsync` istisnası** — `DuringCommit` hook commit'ten önce atar (`:252-257`); `WriteTransactionScope.CommitAsync` (`SqlWriteTransactions.cs:69`) hata davranışı test edilmez.
3. **Gerçek ikinci participant'ın yazdıktan sonra hatası** — writer failure testleri fake writer iledir (`CashAdvanceHandlerTests.cs:257-276`); `NextCancelFailure` hiç kullanılmaz.
4. **Handler içinde, bir modül yazdıktan sonra fırlatılan gerçek uygulama exception'ı** — en yakın kanıt hook tabanlı S5/S18/S22.
5. **Self-commit bypass'ın davranışsal testi** — no-op wrapper fake ile test edilir; host scope altında handler commit'inin gerçekten no-op olduğunu ayrı connection'dan gözlemleyen test yok; production `HostTransactionScope` dalı hiç çalıştırılmaz.
6. **Tekrarlanan participant kaydı** — `ReferenceEquals` guard'ı (`UnitOfWork.cs:20-23`) test edilmez.
7. **Açık connection/transaction kimlik assert'i** — `Assert.Same(...GetDbConnection())`, `Database.CurrentTransaction == scope.Transaction`, `IsolationLevel.Serializable` doğrulaması yok (testlerdeki tek `Serializable` referansı fake `WriteTransactionInternalsTests.cs:170`).
8. **Üç veya daha fazla modülün tek transaction'ı / iki farklı owner'ın tek istekte yazması** — S25 bunu iddia etse de (aşağıya bakın) kanıtlamaz.
9. **S25'in iddiası ile içeriği uyuşmuyor** — `IdempotencyCoreScenariosTests.cs:307-334`: dört POST, her biri ayrı `Guid.NewGuid()` anahtar ile; dolayısıyla **dört ayrı host transaction**. Test adı ("Dört modül de host scope'una katılır") yanıltıcıdır; yalnızca her modülün POST'unun middleware altında çalıştığını gösterir. **Dört modülün tek transaction'a katıldığına dair kanıt olarak gösterilmemelidir.**
10. **Docker olmadan çalıştırma** — `ApiFixture.InitializeAsync` container start eder (`:28`); skip/trait guard yok. Bu denetimde integration testleri çalıştırılmadı.

---

## 6. Mimari Karar Önerisi

### Seçenek A — Mevcut yaklaşımları korumak

- **Fayda:** Risk alınmaz; host yolu zaten atomik ve test edilmiş (S18 + modül SQLite testleri). Y-3 farkı host'ta etkisizleştirilmiş olduğu için "fark korunabilir" tezi savunulabilir.
- **Maliyet/dezavantaj:** İki yazma yolu (modül-yerel `ITransactionScopeFactory` ve shared `IWriteTransactionScopeFactory`) yaşamaya devam eder; `committed` ölü flag'i, iki commit noktası, güncel olmayan yorum (`CashAdvanceHandlers.cs:12`) ve S25 gibi yanıltıcı testler kalır. Yeni modül/endpoint ekleyen geliştirici için hangi yolun doğru olduğu belirsizliğini korur. Zorlayıcı test olmadığı için drift riski artar.
- **Değerlendirme:** Kısa vadede kabul edilebilir; "fark sorun değil" demek için bile kanıt (enforcement testleri) eksiktir.

### Seçenek B — Ortak transaction participant standardı

- **Fayda:** Dört modül zaten aynı `Enlist` gövdesini kullanıyor; geri kalan tek sapma Expenses/CreditCards'taki modül-yerel fabrikadır. Onu kaldırıp tüm cross-module handler'ları shared `IWriteTransactionScopeFactory`'e geçirmek tek koordinasyon dili sağlar; Y-3 kalıntısı ortadan kalkar.
- **Maliyet/risk:** `ExpenseHandlers.cs:95,181`, `RefundHandlers.cs:25,75`, `StatementHandlers.cs:57` ve ilgili test factory'lerine dokunmak gerekir; bu bir refactor'dur ve bu denetimin kapsamı dışıdır. Mevcut kodda **kanıtlanmış bir atomiklik hatası olmadığı için** bu değişikliğin aciliyeti düşüktür; regresyon riski faydasını aşabilir.
- **Değerlendirme:** Yalnızca A'daki belirsizlik kalıcı hâle gelirse veya yeni modüller gerçekten farklı bir yol kullanmaya başlarsa gündeme alınmalı; şu an için **kanıtlarla haklı çıkarılamaz**.

### Seçenek C — Host seviyesinde merkezi koordinasyon

- **Fayda:** Mevcut kod zaten bu yöndedir — `Program.cs:60-72`, `IdempotencyMiddleware.cs:141-155,207,259,285-315`, `SqlWriteTransactions.cs:80-87`. "Daha açık ve standart" hâle getirmek, düşük riskli adımlarla mümkün: (1) host-owned commit'i zorunlu kılan mimari test, (2) `committed` ölü flag'ini kaldırma veya kullanma, (3) iki commit noktasını tek bir yardımcı metotta birleştirme, (4) connection/transaction kimliğini assert eden testler, (5) S25'i düzeltme, (6) `CashAdvanceHandlers.cs:12` yorumunu güncelleme.
- **Maliyet:** Çoğunlukla test ve küçük temizlik; handler iş mantığına dokunmaz. En yüksek fayda/en düşük risk oranı bu seçenektedir.
- **Değerlendirme:** Kanıtlar bu seçeneği destekler. Merkezi koordinasyon zaten var; eksik olan şey **onun zorunlu kılınması ve doğrulanmasıdır**, yeni bir soyutlama değil.

### Önerilen yaklaşım

**Seçenek C — mevcut host-merkezi modeli pekiştirmek (birincil öneri).**

Gerekçe:
- Y-3 farkı gerçektir ancak host yolunda atomikliği etkilemez; bu nedenle B'ye yönelik acil bir refactor için **kanıt yoktur**.
- Atomiklik, incelenen cross-module akışlar için kod yapısı ve gerçek DB testleriyle **desteklenmektedir**; ancak "tüm hata senaryolarında kesin atomik" iddiası için **kanıt eksiktir** (bölüm 4/5). Bu eksikliği kapatmak da C kapsamındadır.
- A'yı olduğu gibi korumak, ölü kod ve yanıltıcı testler nedeniyle gelecek hataları yakalamaz.

**Başarı kriterine doğrudan cevap:**
- *"Farklı modüllerin birlikte gerçekleştirdiği finansal yazma işlemleri hata durumlarında gerçekten atomik mi?"* — **İncelenen iki akış (cards+cash inflow, debts+cash inflow) için evet:** tek connection + tek `DbTransaction` + no-op handler commit + host commit, gerçek SQLite ve gerçek MSSQL (S18) testleriyle desteklenir. **Genel olarak kanıtlanmış değildir:** gerçek ikinci participant hatası, gerçek commit istisnası, Enlist hatası, 3+ modül ve connection/transaction kimlik assert'i test edilmemiştir. Bu yüzden "evet" ifadesi *desteklenen* ancak *kapsamlı kanıtlanmamış* bir sonuçtur.
- *"Participant farklılıkları korunabilir mi, yoksa ortak standarda mı ihtiyaç var?"* — **Korunabilir, ancak koşullu:** fark host yolunda etkisizleştirilmiş bir miras olduğu için acil standartlaştırma (B) gerekmiyor; buna karşılık C kapsamındaki zorlayıcı test/temizlik adımları yapılmazsa fark bir risk kaynağına dönüşebilir.

---

## 7. Sonraki Adım

Aşağıdaki görevler **uygulanmayacak**, yalnızca plan olarak sunulmuştur. Her biri küçük ve bağımsızdır.

### Görev 1 — Host-owned commit'i zorunlu kılan mimari test
- **Amaç:** Bir modülün gerçek transaction açıp commit etmesini / host koordinasyonunu atlamasını tespit etmek.
- **Olası katmanlar:** `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs`.
- **Beklenen davranış:** Dört `UnitOfWork` için `IWriteScopeParticipant` zaten var; buna ek olarak modüllerin `Microsoft.Data.SqlClient`/`Database.BeginTransaction` kullanmadığını ve handler'ların `IWriteTransactionScopeFactory.BeginAsync` dışında gerçek transaction kaynağına yönelmediğini doğrulayan kaynak-metin/IL kontrolü (CashInflows/Debts için de no-op dalının olmadığını, shared fabrikayı kullandığını söyleyen pozitif assert).
- **Gerekli testler:** Yeni mimari test(ler) + mevcut `:89-135` testlerinin genişletilmesi.
- **Mimari riski:** Kaynak-metin testleri kırılgandır; yanlış pozitif üretmemek için mevcut `:110-135` desenini izlemeli.

### Görev 2 — Commit sahipliğini tek noktada toplama ve ölü flag temizliği
- **Amaç:** `committed` ölü değişkenini ve iki ayrı `scope.CommitAsync` çağrısının belirsizliğini gidermek.
- **Olası katmanlar:** `src/Host/FinTrack.Api/Idempotency/IdempotencyMiddleware.cs` (`:143,160,198,259-260,344-345`).
- **Beklenen davranış:** Commit + tombstone geçişi tek bir yardımcı metot üzerinden yapılır; `committed` ya kaldırılır ya da gerçekten bir karar için kullanılır; davranış değişmez (2xx → Completed + commit, expired → Tombstone + commit).
- **Gerekli testler:** Mevcut S5/S6/S8/S18/S21 regresyon seti (Docker) + `IdempotencyFailureAndLifecycleTests.cs` TTL testleri.
- **Mimari riski:** Orta — commit sırası/zamanlaması değiştirilmemeli; hook noktaları (`:247-266`) korunmalı.

### Görev 3 — Connection/transaction kimliğini doğrulayan testler
- **Amaç:** "Aynı DbTransaction" iddiasını fixture kurulumu olmaktan çıkarıp test edilen bir invariant hâle getirmek.
- **Olası katmanlar:** `tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs`, `tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs`, `tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs`.
- **Beklenen davranış:** `Assert.Same` ile owner DbConnection == writer DbConnection; `Database.CurrentTransaction` / `GetEnlistedTransaction()` == `scope.Transaction`; `IsolationLevel.Serializable` (gerçek scope için) doğrulanır.
- **Gerekli testler:** Yeni assert'ler; mevcut rollback testleri korunur.
- **Mimari riski:** Düşük; yalnızca test kodu.

### Görev 4 — Gerçek participant/commit hata senaryoları
- **Amaç:** Bölüm 4.2/4.3/4.5'teki kanıt boşluklarını kapatmak.
- **Olası katmanlar:** modül testleri (`CountingUnitOfWork` yerine gerçek writer'da hata tetikleyen bir mekanizma) ve integration test hook'ları (`IdempotencyTestHooks.cs`).
- **Beklenen davranış:** (a) ikinci participant gerçekten yazdıktan sonra hata verdiğinde ilk modülün satırı yok; (b) `Enlist` istisnasında 500 + 0 finans satırı; (c) `DbTransaction.CommitAsync` seviyesinde gerçek istisna simülasyonu; (d) handler içinde exception → tüm yazılar rollback.
- **Gerekli testler:** En az 4 yeni test (2 modül SQLite + 1-2 MSSQL/Docker).
- **Mimari riski:** Orta — test hook'larının üretim kodu üzerindeki etkisi dikkatle sınırlanmalı (mevcut singleton hook kaydı `Program.cs:74`).

### Görev 5 — S25 düzeltmesi ve yanıltıcı yorum temizliği
- **Amaç:** Yanıltıcı kanıtları ortadan kaldırmak.
- **Olası katmanlar:** `tests/FinTrack.Api.IntegrationTests/IdempotencyCoreScenariosTests.cs:301-342`, `src/Modules/CreditCards/FinTrack.Modules.CreditCards.Application/CashAdvances/CashAdvanceHandlers.cs:12`.
- **Beklenen davranış:** S25 ya gerçek tek-transaction senaryosuna dönüştürülür (tek key + host hook + 4 modül yazısı — uygulanabilirlik ayrıca değerlendirilir) ya da adı "her modülün POST'u middleware altında çalışır" olacak şekilde düzeltilir; yorum ADR-0010'yı yansıtacak şekilde güncellenir.
- **Gerekli testler:** Test adı/gövdesi değişimi; davranış değişikliği gerekmez.
- **Mimari riski:** Düşük.

### Görev 6 (opsiyonel, yalnızca Görev 1-5 sonrası) — Y-3 mirasını birleştirme (Seçenek B)
- **Amaç:** Expenses/CreditCards'taki modül-yerel `ITransactionScopeFactory` yolunu shared fabrikaya indirgemek.
- **Olası katmanlar:** `Expenses/CreditCards UnitOfWork.cs:51-88`, `ExpenseHandlers.cs:95,181`, `RefundHandlers.cs:25,75`, `StatementHandlers.cs:57`, ilgili test factory'leri.
- **Beklenen davranış:** Davranış değişmez; tek koordinasyon dili kalır.
- **Gerekli testler:** Tüm modül + API regresyon seti.
- **Mimari riski:** **Yüksek** — kanıtlanmış bir hata olmadan yapılan geniş refactor; bu nedenle sonraya bırakılmalı.

---

## 8. Audit Bulguları

| ID | Seviye | Tür | Kanıt | Etki | Öneri |
|---|---|---|---|---|---|
| **F-01** | **Orta** | Doğrulanmış yapısal bulgu (Y-3) | `Expenses/CreditCards .../UnitOfWork.cs:51-88` modül-yerel `TransactionScopeFactory` + `HostTransactionScope`; `CashInflows/Debts .../UnitOfWork.cs:9-48` yalnızca `Enlist`/`SaveChanges`. | İki koordinasyon dili var; host yolunda etkisiz olsa da geliştirici için belirsizlik ve drift riski. | B'ye hemen geçmeyin; Görev 1 ile zorlayıcı test ekleyin, Görev 6'yı sonraya bırakın. |
| **F-02** | **Düşük** | Doğrulanmış (Y-3'ün atomikliğe etkisi **yok**) | Dört `Enlist` gövdesi aynı (`UnitOfWork.cs:16-28/15-27`); no-op wrapper'lar (`SqlWriteTransactions.cs:80-87`, `HostTransactionScope:74-79`); tek gerçek commit `IdempotencyMiddleware.cs:259/:345`. | Yanlış "Y-3 atomiklik hatasıdır" yargısının önüne geçer. | Rapor sonucunda net biçimde "yapısal fark, atomiklik hatası değil" olarak belgeleyin. |
| **F-03** | **Orta** | Doğrulanmış kod kokusu | `committed` atanır ama okunmaz: `IdempotencyMiddleware.cs:143,160,198,260`. | Ölü state; commit kararının tek kaynak noktası olmadığı izlenimi. | Görev 2. |
| **F-04** | **Orta** | Doğrulanmış tutarsızlık | ADR-0010 "tek commit" ifadesine karşın iki commit noktası: middleware `:259` ve `:345`. | İki dalın farklı test edilmesi / gelecekte tutarsız davranış riski. | Görev 2 (tek yardımcı metot), davranış korunarak. |
| **F-05** | **Yüksek** | Doğrulanmış test kapsam açığı (yanıltıcı test) | `IdempotencyCoreScenariosTests.cs:301-342` S25: dört ayrı `Guid.NewGuid()` anahtarlı POST → dört ayrı transaction; isim "dört modül aynı scope" diyor. | Dört modül atomikliği yanlışlıkla kanıtlanmış sayılabilir; karar vericileri yanıltır. | Görev 5; S25 kanıt olarak gösterilmesin. |
| **F-06** | **Yüksek** | Potansiyel risk (kanıtlanmamış davranış) | `Enlist(scope)` döngüsü middleware'de try/catch'siz (`:152-155`); detach döngüsünde var (`:289-299`). `Enlist` istisnası test edilmez. | Participant kaydı sırasında hata → 500, idem kaydı rollback; veri atomik görünse de graceful davranış belirsiz. | Görev 4(a); middleware'de kayıt döngüsü için hata politikası netleştirilmeli. |
| **F-07** | **Yüksek** | Potansiyel risk (kanıtlanmamış davranış) | Gerçek `DbTransaction.CommitAsync` istisnası hiç tetiklenmez; `DuringCommit` hook commit'ten önce atar (`IdempotencyMiddleware.cs:252-257`); `WriteTransactionScope.CommitAsync` (`SqlWriteTransactions.cs:69`) doğrudan delege. | Commit sonrası belirsizlik stratejisi gerçek commit hatasında doğrulanmamış. | Görev 4(c). |
| **F-08** | **Orta** | Doğrulanmış kapsam açığı (gerçek participant hatası) | `CashAdvanceHandlerTests.cs:257-276` fake writer kullanır; `NextCreateFailure` yalnızca burada, `NextCancelFailure` hiç kullanılmaz; writer fake'i DB'ye yazmaz (`WriteTransactionTestDoubles.cs:69-86`). | "İkinci participant hata verdiğinde ilk geri alınır" iddiası gerçek ikinci yazıcı için kanıtlanmadı. | Görev 4(a). |
| **F-09** | **Orta** | Doğrulanmış kapsam açığı (kimlik invariant'ı) | Hiçbir test `Assert.Same(GetDbConnection())` / `CurrentTransaction == scope.Transaction` / `Serializable` assert'i yapmaz; fixture kurulum gerçeği (`CardTestDatabase.cs:29-35,53-62`, `DebtTestDatabase.cs`). | "Aynı transaction" bir test edilen invariant değil; regresyonda sessizce bozulabilir. | Görev 3. |
| **F-10** | **Orta** | Doğrulanmış kapsam açığı (enforcement) | `AtomicWriteBoundaryTests.cs:110-135` yalnızca Expenses/CreditCards için **kaynak-metin** no-op kontrolü; host-owned commit'i zorunlu kılan test yok; `Commit`/`owner` için architecture test grep'i boş. | Yeni modül/host dışı yazma yolu eklendiğinde otomatik yakalama yok. | Görev 1. |
| **F-11** | **Orta** | Potansiyel risk (host kapsamı dışı yollar) | PUT POST-only kuralıyla dışarıda (`IdempotencyMiddleware.cs:378-392`); auth POST hariç (`:386-389`); kimlik hatasında `:76-80` host scope açılmadan `next`. Bu yollarda yazma olabileceğine dair test yok. | Gelecekte bu yollara eklenen finansal yazmalar host atomicliğinden yoksun kalabilir. | Görev 1 kapsamında allowlist/enforcement; `:76-80` dalı için davranış testi. |
| **F-12** | **Düşük** | Doğrulanmış yeniden kayıt güvenliği + kapsam açığı | `ReferenceEquals(_enlisted, transaction)` guard'ı dört UoW'da var (`UnitOfWork.cs:20-23`), middleware de-duplication yapmıyor (`:152-155`), ama guard test edilmiyor. | Tekrar enlist güvenli görünüyor; ancak regresyona karşı koruma yok. | Görev 3/4 kapsamında çift-enlist testi. |
| **F-13** | **Düşük** | Doğrulanmış belge/kod sapması | `CashAdvanceHandlers.cs:12` yorumu "commit yalnızca handler'dadır" diyor; ADR-0010 altında gerçek commit host'ta (`:259`). | Yanlış zihinsel model; bakım maliyeti. | Görev 5. |
| **F-14** | **Düşük** | Doğrulanmış kapsam açığı (rollback mekanizması) | Açık `RollbackAsync` yok; rollback `DbTransaction.DisposeAsync()` yorumuna dayanır (`SqlWriteTransactions.cs:71-72`), integration testleri Docker'a bağımlı ve bu denetimde **çalıştırılmadı** (`ApiFixture.cs:16-28`). | Rollback garantisi framework davranışına bağlı; kaynak kodda kanıtlanamaz. | Görev 4 + Docker erişilebilir ortamda test çalıştırması (operatör), raporda "çalıştırılmadı" notu korunsun. |

---

**Genel sonuç:** FinTrack'te incelenen modüller arası finansal yazmalar, host-merkezi koordinasyon sayesinde hata durumlarında **desteklenen bir atomiklik** sergiliyor; Y-3 farkı gerçek bir yapısal fark olmakla birlikte host yolunda atomikliği bozan bir etkisi **kanıtlanmadı**. Buna karşılık bir dizi **kanıtlanmamış hata senaryosu** ve **yanıltıcı/zayıf test** mevcut olduğundan, "kesin atomik" veya "fark sorunsuz korunabilir" iddiaları şu an için güvenilir değildir. Önerilen yol, yeni bir soyutlama (B) yerine mevcut host koordinasyonunu (C) test ve küçük temizliklerle pekiştirmektir. Bu denetimde **kod değiştirilmedi, test çalıştırılmadı, commit/push yapılmadı.**

---

## 9. V12-T01 — Host Transaction Ownership Enforcement Tests (2026-11-21 eki)

Bu bölüm yalnızca **sonradan tamamlanan test değişikliklerini** kaydeder; yukarıdaki
denetim bulguları ve sonuçları değişmemiştir. Production koduna dokunulmadı,
yeni abstraction/interface eklenmedi, transaction fabrikası refactor edilmedi,
NuGet paketi/migration eklenmedi, commit/push yapılmadı.

### 9.1 Değiştirilen test dosyaları

| Dosya | Değişiklik |
|---|---|
| `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs` | 4 yeni test/genişletme (bkz. 9.2) |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs` | Gerçek SQLite enlistment kimlik testi eklendi |
| `tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs` | Gerçek SQLite enlistment kimlik testi eklendi |
| `tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs` | Host fabrikasında `Serializable` doğrulaması + kayıtlı `DbConnection` test çifti |
| `tests/FinTrack.Modules.Expenses.Tests/Application/UnitOfWorkEnlistTests.cs` | **Yeni** — Expenses `Enlist` guard testleri |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/UnitOfWorkEnlistTests.cs` | **Yeni** — CreditCards + CashInflows `Enlist` guard testleri |
| `tests/FinTrack.Modules.Debts.Tests/Application/UnitOfWorkEnlistTests.cs` | **Yeni** — Debts + CashInflows `Enlist` guard testleri |

Not: dört `UnitOfWork` kapsanmıştır. CashInflows, `InternalsVisibleTo` nedeniyle
hem CreditCards hem Debts test projelerinde doğrulanır; Expenses yalnızca kendi
test projesinde doğrulanabilir.

### 9.2 Yeni testlerin doğruladığı mimari garantiler

| Test | Garanti | İlgili bulgu |
|---|---|---|
| `Transaction_sozlesmeleri_buildingblocks_ta_ve_dort_modulde_baglanti_var` | 6 transaction sözleşmesinin `FinTrack.ModuleContracts` içinde tanımlı olduğu ve dört modül Infrastructure derlemesinin bu contract'a bağlı olduğu | F-10 |
| `UnitOfWork_Enlist_imzasi_ve_host_fallback_kaynakta_korunuyor` | Dört UoW'da `Enlist(IWriteTransactionScope)` imzası, `scopeAccessor?.Current` fallback'i ve `UseTransaction` katılım mekanizması | F-01, F-12 |
| `Moduller_ham_SqlClient_bagintisi_veya_transaction_acmasi_yapmiyor` | Dört modülde `new SqlConnection` / `SqlConnection(` / `new SqlTransaction` / `SqlTransaction(` olmadığı (ham SqlClient ile host atlama yok) | F-01, F-10 |
| `Transaction_baslatma_sadece_mevcut_yerel_fabrikada` | CashInflows/Debts'ta hiç `BeginTransaction` olmadığı; Expenses/CreditCards'ta ise yalnızca mevcut yerel `TransactionScopeFactory` içinde ve host-scope guard'ıyla başlatıldığı — **mevcut tasarım ihlal sayılmadı, yanlış pozitif üretilmedi** | F-01, F-10 |
| `Enlist_sirada_owner_ve_writer_ayni_fiziksel_transaction_a_bagli` (2 dosya) | Gerçek SQLite'ta owner ve writer DbContext'lerinin `Database.GetDbConnection()` referansları eşit; her iki `Database.CurrentTransaction.GetDbTransaction()` değeri `scope.Transaction` ile aynı referans; enlist sonrası bağlantı kullanılabilir | **F-09** |
| `Gercek_host_fabric_bagantiyi_Serializable_transaction_ile_acar` | Production `SqlWriteTransactionScopeFactory` kapalı bağlantıyı açıyor, `IsolationLevel.Serializable` istiyor ve gerçek `WriteTransactionScope` döndürüyor — SQL Server/Docker olmadan | F-09 |
| `Ayni_transaction_icin_ikinci_enlist_baglantiyi_bozmaz` (dört modül) | Aynı transaction ikinci kez enlist edildiğinde `CurrentTransaction` ve `_enlisted` referansı değişmiyor; transaction hâlâ commit edilebiliyor | **F-12** |
| `Enlist_null_host_scope_yoksa_transaction_dan_ayrir` (dört modül) | Accessor yokken `Enlist(null)` DbContext'i transaction'dan ayırıyor | F-12 |
| `Enlist_null_host_scope_varsa_hosta_duser_sonra_temizlenince_ayrilir` (dört modül) | Host accessor doluyken `Enlist(null)` host transaction'ına düşüyor; accessor temizlenince `Enlist(null)` ayırıyor | F-12 |

### 9.3 Doğrulama sonuçları

- `dotnet build FinTrack.slnx --no-restore`: **0 uyarı / 0 hata**.
- Çalıştırılan (Docker gerektirmeyen) testler:
  - Architecture **17/17**, CreditCards **85/85**, Debts **38/38**,
    Api.UnitTests **78/78**, Expenses **58/58**, CashInflows **48/48**,
    Reporting **10/10**, Mobile **45/45**.
  - Toplam: **379/379** geçti, 0 başarısız, 0 atlanan.
- Docker/Testcontainers gerektiren `FinTrack.Api.IntegrationTests` **çalıştırılmadı**
  (S18/S25/S5/S6/S21 dahil tüm gerçek MSSQL senaryoları doğrulanmadı).

### 9.4 Testlerin kanıtlamadığı davranışlar

- `Enlist` guard'ının `UseTransaction` çağrısını **kaç kez** yaptığı doğrudan sayılmadı;
  "gereksiz yeniden enlist yok" iddiası `_enlisted` referansı + `CurrentTransaction`
  kararlılığı + başarılı commit ile dolaylı olarak desteklenir.
- SQLite test çifti `IsolationLevel` belirtmediği için SQLite tarafında `Serializable`
  iddiası test edilmedi; yalnızca **production host fabrikasının** `Serializable`
  istediği fake connection ile kanıtlandı. Gerçek SQL Server davranışı (S18 vb.)
  Docker olmadan doğrulanamadı.
- Kaynak-metin tabanlı mimari testleri kırılgandır; yeni dosya/desene göre
  güncellenmeleri gerekir. Çalışma zamanındaki tüm olası host atlama yollarını
  kapsamaz.
- F-02…F-08, F-11, F-13, F-14 bu görevde kapsanmadı. F-06/F-07/F-08 için §9.6'da
  ek testler eklendi; buna karşın 3+ modülün tek transaction'ı, gerçek MSSQL
  commit davranışı ve F-06 hata politikası kararı açık kalmaya devam eder.

### 9.5 Audit bulgularının durumu (V12-T01 anlık görüntüsü)

> Bu tablo V12-T01 sonrasını kaydeder. V12-T02 ile kapatılan bulgular ve
> korunan açık/kısmen kapalı durumlar için **§10.3** esas alınmalıdır.

| Bulgu | V12-T01 sonrası durum |
|---|---|
| **F-01** (Orta) | Kısmen kapatıldı: sapma tespiti zorlayıcı hale getirildi; yapısal fark korunuyor (kasıtlı). |
| **F-06** (Yüksek) | Kısmen kapatıldı (§9.6): mevcut davranış testle belgelendi — `Enlist` istisnası yayılır (500), idempotency kaydı yazılmaz, scope kapanır. Politika kararı açık. |
| **F-07** (Yüksek) | Kısmen kapatıldı (§9.6): gerçek `DbTransaction.CommitAsync` istisnasının production scope'tan yayıldığı ve middleware'da 409'a çevrildiği test edildi; gerçek MSSQL commit belirsizliği açık. |
| **F-08** (Orta) | Kısmen kapatıldı (§9.6): gerçek ikinci yazıcı SQLite'a yazıp sonra hata verdiğinde commit olmadığı ve iki modülün satırının bulunmadığı doğrulandı; 3+ modül açık. |
| **F-09** (Orta) | Kısmen kapatıldı: connection/transaction kimliği ve host `Serializable` isteği test edildi; 3+ modül tek transaction'ı hâlâ kanıtlanmadı. |
| **F-10** (Orta) | Kısmen kapatıldı: dört modül için mimari koruma eklendi; PUT/auth kapsam dışı yollar açık. |
| **F-12** (Düşük) | Kapatıldı: guard davranışı dört modül için test edildi. |
| F-02, F-03, F-04, F-05, F-11, F-13, F-14 | Açık (bu görev kapsam dışı). |

### 9.6 F-06/F-07/F-08 hata sınırı testleri (2026-11-21, ikinci ek)

Production kodu değişmedi. Yalnızca test eklendi; `FinTrack.Api.UnitTests`
projesine test amaçlı `Microsoft.EntityFrameworkCore.Sqlite` paket referansı
eklendi (host/migration değişmedi).

| Test | Bulgu | Doğrulanan davranış |
|---|---|---|
| `Enlist_istisnasi_mevcut_politikada_yayilir_ve_hicbir_kayit_yazilmaz` | F-06 | Middleware kayıt döngüsü try/catch'siz: istisna yayılır (HTTP 500), store insert/complete yapılmaz, scope kapanır, tüm participant'lar detach edilir. **Politika değişikliği istenirse bu test güncellenir.** |
| `Gercek_commit_istisnasi_409_InProgress_doner_ve_scope_kapanir` | F-07 | Commit istisnası yakalanır, 409 `IdempotencyInProgress` döner; `CompleteAsync` çağrılır ama transaction commit edilmez, scope kapanır. |
| `WriteTransactionScope_DbTransaction_CommitAsync_istisnasini_yayar` | F-07 | Production `WriteTransactionScope`, gerçek `DbTransaction.CommitAsync` istisnasını sarmaz; aynı istisna dışa yayılır. |
| `Ikinci_yazici_gercekten_yazip_sonra_hata_verdiginde_kart_satiri_yok` | F-08 | Fake writer yerine gerçek `DebtCashInflowWriter` SQLite'a yazar, ardından hata döndürülür; commit 0, kart ve para girişi satırı yok. |

Doğrulama: çözüm build 0 uyarı/0 hata; Api.Unit **81/81**, CreditCards **86/86**;
Docker hariç toplam **383/383** (önceki 379/379 + 4 yeni test). Docker/Testcontainers (gerçek MSSQL) senaryoları
çalıştırılmadı; F-06 politika kararı ve 3+ modül tek-transaction kanıtı açık kaldı.

---

## 10. V12-T02 — Commit yolu temizliği ve kapsam sınırı testleri (2026-11-21 eki)

Bu bölüm V12-T02'nin **gerçek Git diff'i** (`HEAD` `1826dc7` ↔ çalışma kopyası)
ve çalıştırılmış test sonuçlarıyla doğrulanan kapanışları kaydeder. Beş kod/test
dosyası değiştirilmiştir; bunlar **henüz commit/push edilmemiştir**. Son yeşil CI
hâlâ `44931169879` (`build-test`, commit `1826dc7`) olup V12-T02 değişikliklerini
içermez. Windows manuel UI smoke bu turda da yapılmadı.

### 10.1 Değiştirilen kod/test dosyaları

| Dosya | Değişiklik |
|---|---|
| `src/Host/FinTrack.Api/Idempotency/IdempotencyMiddleware.cs` | Okunmayan `committed` state'i kaldırıldı; 2xx ve expired→Tombstone commit çağrıları `CommitStatusChangeAsync` altında toplandı. `HandleExistingAsync` `Task<bool>` yerine `Task` döndürüyor. |
| `src/Modules/CreditCards/FinTrack.Modules.CreditCards.Application/CashAdvances/CashAdvanceHandlers.cs` | Sınıf yorumu düzeltildi: ADR-0010 host kapsamında gerçek commit middleware'da; host yoksa ADR-0009 akışı korunur. Çalışma zamanı kodu değişmedi. |
| `tests/FinTrack.Api.IntegrationTests/IdempotencyCoreScenariosTests.cs` | S25'in adı ve XML yorumu dört bağımsız POST/anahtar/transaction gerçeğini yansıtacak şekilde düzeltildi. |
| `tests/FinTrack.Api.UnitTests/IdempotencyFailureBoundaryTests.cs` | Expired→Tombstone için `Sureti_dolan_kayit_tombstone_yazip_tek_commit_ve_409_doner` eklendi; `FakeStore` mevcut kayıtla oluşturulabilir hale getirildi. |
| `tests/FinTrack.Api.UnitTests/IdempotencyScopeBoundaryTests.cs` | **Yeni.** PUT, auth POST ve kimlik hatası için gerçek `IdempotencyMiddleware.InvokeAsync` çağrıları; kapsam içi POST için pozitif kontrol. |

### 10.2 Kapatılan bulgular ve kanıtları

| Bulgu | Önceki durum | Yapılan değişiklik | Doğrulama / kapsam |
|---|---|---|---|
| **F-03** | `committed` atanır, hiç okunmaz (`IdempotencyMiddleware.cs:143,160,198,260`). | Flag ve `HandleExistingAsync` dönüş değerindeki ölü `bool` kaldırıldı. | `git diff HEAD`: state tanımı ve tüm atamalar gitti; çağrı noktaları `await HandleExistingAsync(...)` oldu. Commit/hata davranışı değişmedi. |
| **F-04** | İki doğrudan commit: 2xx (`:259`) ve expired→Tombstone (`:345`). | Her iki dal `CommitStatusChangeAsync(writeStatus, scope, cancellationToken)` çağırıyor. Hook sırası yalnızca 2xx yolunda geçerlidir: `CompleteAsync → BeforeCommitAsync → CommitMode kontrolü → CommitAsync → AfterCommit`; expired→Tombstone dalı `TombstoneAsync → CommitAsync` çağırır ve `BeforeCommitAsync`, `CommitMode` hook kontrolü veya `AfterCommit` içermez. | `Sureti_dolan_kayit_tombstone_yazip_tek_commit_ve_409_doner`: `TombstoneCount=1`, `CommitCount=1`, `CompleteCount=0`, `InsertCount=0`, `nextCalls=0`, 409. 2xx yolu için mevcut middleware testleri korundu. Hata/rollback sahipliği çağrana ait: 2xx 409'a çevirir, expired dalı yayar. |
| **F-05** | S25 adı "dört modül aynı scope" diyordu; gövde dört ayrı `Guid.NewGuid()` kullanıyordu. | Test adı `S25_Dort_modulun_POST_u_idempotency_middleware_altinda_calisir` oldu; yorum dört ayrı host transaction'ını açıkça yazıyor; test gövdesi korundu. | **Dört ayrı POST, dört ayrı `Idempotency-Key` ve dört ayrı transaction söz konusudur. Bu test dört modülün tek bir ortak transaction içinde atomik çalıştığını KANITLAMAZ.** Gerçek Docker/MSSQL entegrasyon koşusunda 95/95 içinde yer aldı. |
| **F-11** | PUT/auth POST ve kimlik hatasında host scope açılmadığı yalnızca kaynak koddan çıkarılıyordu; davranış testi yoktu. | `IdempotencyScopeBoundaryTests.cs` eklendi. Geçersiz anahtar bilerek gönderilerek middleware kapsam içinde olsaydı 400 üreteceği durum kuruldu. | Theory 4 vaka: `PUT /api/v1/expenses/abc`, `PUT /api/v1/categories/abc/archive`, `POST /api/v1/auth/login`, `POST /api/v1/auth/register` → `next=1`, 200, `BeginCount=0`, `SetCount=0`, participant enlist/detach=0, store erişimi=0, commit=0, cleanup=0. Kimlik hatası ayrı testte aynı sonucu doğruluyor. Pozitif kontrol kapsam içi POST'ta 1 scope/1 commit görüyor. **Bu testler middleware'in scope açmadığını ve isteği doğrudan devam ettirdiğini kanıtlar; PUT/auth POST endpoint'lerinde domain write bulunmadığını kanıtlamaz.** |
| **F-13 (audit kimliği)** | `CashAdvanceHandlers.cs:12` yorumu "commit yalnızca handler'dadır" diyordu. | Yorum ADR-0010 host sahipliğini ve hostsuz ADR-0009 akışını doğru tarif edecek şekilde düzeltildi. | `git diff HEAD` yalnızca yorum satırlarını gösteriyor; `SqlWriteTransactions.cs:43-49,84-86` no-op zinciri ve `Program.cs:61-66` DI kaydıyla uyumlu. Çalışma zamanı davranışı değişmedi. |

> **Kimlik uyarısı:** Görev metninde "F-13 — expired→Tombstone tek commit regression testi"
> ifadesi kullanılmıştır. Audit raporundaki **F-13** ise güncel olmayan
> `CashAdvanceHandlers` yorumudur (yukarıdaki satır). Expired→Tombstone tek commit
> davranışının regression kanıtı bu raporda **F-04** altında kaydedilmiştir. İki
> id aynı çalışmayı temsil etmez; ikisi de yukarıda kapatılmıştır.

### 10.3 Audit bulgularının güncel durumu (V12-T02 sonrası)

| Bulgu | Durum | Not |
|---|---|---|
| F-01 | Kısmen kapalı (korundu) | Y-3 yapısal farkı bilinçli olarak korundu; atomicity hatası kanıtlanmadı. |
| F-02 | Doğrulanmış (korundu) | Tek gerçek commit artık `CommitStatusChangeAsync` içinde. |
| **F-03** | **Kapatıldı** | Ölü `committed` state'i kaldırıldı. |
| **F-04** | **Kapatıldı** | İki commit yolu tek yardımcı metotta birleşti; davranış korundu. |
| **F-05** | **Kapatıldı** | S25 adı/gerekçesi dört ayrı transaction gerçeğiyle uyumlu. |
| F-06 | Kısmen kapalı (korundu) | Mevcut politika testle belgelendi; 500/idempotency kaydı yok/scope kapalı. Politika kararı açık. |
| F-07 | Kısmen kapalı (korundu) | Gerçek commit istisnası 409'a çevriliyor; gerçek MSSQL commit belirsizliği açık. |
| F-08 | Kısmen kapalı (korundu) | İki modüllü gerçek yazıcı senaryosu var; 3+ modül açık. |
| **F-09** | **Açık / bu turda kapatılmadı** | Gerçek connection/transaction identity invariant'larının kapsamı korundu; yeni doğrulama yapılmadı. |
| **F-10** | **Açık / bu turda kapatılmadı** | Host-owned commit'in mimari/test güvencesi bu turda genişletilmedi. |
| **F-11** | **Kapatıldı (sınırlı kapsamda)** | Middleware bypassı ve scope açılmadığı kanıtlandı; endpoint içi domain write iddiası yok. |
| **F-12** | **Bu turda kapatılmadı** | V12-T01 kaydında "Kapatıldı" durumundadır; V12-T02'de yeniden test edilmedi veya durumu değiştirilmedi. |
| **F-13** | **Kapatıldı** | Audit kimliğindeki güncel olmayan yorum düzeltildi; görev metnindeki expired→Tombstone kanıtı F-04'e bağlandı. |
| **F-14** | **Açık / bu turda kapatılmadı** | Commit öncesi/commit sırasında gerçek MSSQL rollback davranışı doğrulanmadı. |

### 10.4 Doğrulama sonuçları

| Kontrol | Sonuç | Kapsam |
|---|---|---|
| `dotnet build FinTrack.slnx --no-restore` | ✅ 0 uyarı, 0 hata | V12-T02 çalışma ağacı. |
| Docker gerektirmeyen testler | ✅ **390/390** | API birim + modül + mimari + mobil; ayrı bir gruptur. |
| `tests/FinTrack.Api.IntegrationTests` | ✅ **95/95** | Gerçek Docker/Testcontainers + MSSQL; 390/390 ile aynı testler **iki kez sayılmadı**. |
| F-06 özel testi | ✅ **1/1** | `Enlist_istisnasi_mevcut_politikada_yayilir_ve_hicbir_kayit_yazilmaz`. |
| `git diff --check` | ✅ Temiz | — |
| Windows manuel UI smoke | ⏳ Yapılmadı | Operatör görevi. |
| Yeni CI | ⏳ Yok | Son yeşil: `44931169879`, commit `1826dc7`, V12-T02 öncesi. |
| Commit / push | ⏳ Yapılmadı | Beş kod/test dosyası çalışma ağacında. |

Docker daemon bu turda erişilebilir olduğu için API entegrasyonları gerçek
Testcontainers/MSSQL ortamında çalıştırıldı; önceki "Docker kapalı" notu bu tur
için geçerli değildir.

### 10.5 Korunan önceden var olan riskler (V12-T02 regresyonu değil)

Son diff doğrulamasında aşağıdakilerin V12-T02 tarafından oluşturulmadığı tespit
edildi; mevcut davranış/risk olarak korunur:

1. `OperationCanceledException`, middleware'ın mevcut `catch (Exception)`
   davranışı nedeniyle 409 `IdempotencyInProgress`'e dönüşebilir
   (`IdempotencyMiddleware.cs:270-276`).
2. `RequestAborted` ile cleanup gate beklemesinde iptal oluşabilir
   (`IdempotencyCleanupService.cs:34`); V12-T02 bu dosyaya dokunmadı.
3. Handler içinde `Enlist(null)` hata verirse takip eden `DisposeAsync()`
   çağrısı atlanabilir (`CashAdvanceHandlers.cs:106-107` ve `:196-197`);
   host kapsamında no-op zinciri nedeniyle pratik etki sınırlıdır, hostsuz
   yolda çalışma zamanı davranışı kaynak koddan kesin doğrulanamaz.
4. Rollback, açık `RollbackAsync` yerine uncommitted transaction dispose
   davranışına dayanır (`SqlWriteTransactions.cs:71-72`; F-14 açık).

Ayrıca `Enlist(scope)` istisnasının yayılması (F-06 politikası), 3+ modülün tek
transaction kanıtının bulunmaması ve Windows manuel smoke'un yapılmamış olması
bu turda kapatılmadı. Y-3 yapısal farkı için refactor önerilmedi; atomicity
hatası kanıtlanmadı.
