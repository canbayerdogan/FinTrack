# FinTrack — Görev Listesi (TODO)

**Sürüm:** 1.15
**Tarih:** 2026-11-18
**Bağlı belgeler:** [Durum notu v2.7](08-durum-ve-devam-notu.md) · [Yol haritası v1.14](07-yol-haritasi-ve-ajan-gorev-standardi.md)

> Bu dosya **nerede kaldığımızı** tutar. Bağlayıcı kapsam `docs/07` §4'tedir;
> burada yalnızca ilerleme ve sıradaki somut adımlar listelenir.
>
> **Kural:** Her dilim sonunda kutular işaretlenir, tamamlanan dilim ✅ satırına
> taşınır ve `docs/08` §2–§4 güncellenir. Açık soru yanıtlanmadan kod yazılmaz.

---

## 1. Oturum başlangıç protokolü (tüm repoyu okuma yok)

1. **Bu dosya** → sıradaki dilim + açık maddeler.
2. `docs/08` §2–§4 → güncel durum, kararlar, tuzaklar.
3. `docs/07` §4 ilgili dilim → bağlayıcı kapsam ve kabul kriterleri.
4. Gereken tek tek dosyalar için §7'deki haritayı kullan (modül kopyalama yok).
5. Açık soru varsa **sor**; cevap gelmeden kod yazma.
6. Dilim bitişi: build + test → doküman → bu dosya → `docs/08` → rapor.

**Hızlı durum kontrolü:**

```powershell
dotnet build FinTrack.slnx --nologo -v q
dotnet test FinTrack.slnx --nologo -v q
```

---

## 2. Şu an

| Alan | Değer |
|---|---|
| Aktif dilim | **9 — MAUI istemci** ✅ (tamamlandı; CI yeşil) |
| Son doğrulama | 263/263 birim/mimari (248 backend + 15 mobile) · Windows MAUI build 0/0 · 72/72 entegrasyon · toplam **335/335** · CI run `44365407221` yeşil (CI bakımı sonrası, `ubuntu-24.04`; Slice 9 run `44189257561`) · 2026-11-18 |
| Sıradaki komut | Android emülatörü/SDK kurulduğunda manuel uçtan uca smoke (giriş → harcama → kart → rapor) |

---

## 3. Slice 3 — Kart + hesaplanan bakiye ✅ (2026-11-14)

### 3.1 Planlama

- [x] `docs/05` §5.1–5.3, `docs/06` §6, ADR-0008, `docs/07` §4 okundu
- [x] Mevcut `ICardQuery` sözleşmesi ve CreditCards iskeleti incelendi
- [x] **Açık sorular yanıtlandı** (§3.6)
- [x] Plan onaylandı

### 3.2 Domain (`FinTrack.Modules.CreditCards.Domain`)

- [x] `CreditCard` aggregate: `BankName`, `DisplayName`, `LogoKey?`, `CreditLimit?`,
      `StatementDay?`, `PaymentDueDay?`, `IsArchived`, `ArchivedAtUtc`,
      `CreatedAtUtc`, `RowVersion`
- [x] Validasyon: `bankName`/`displayName` zorunlu ve uzunluk sınırlı;
      `logoKey` opsiyonel; `creditLimit` opsiyonel, > 0, max 4 ondalık;
      `statementDay`/`paymentDueDay` 1–31
- [x] `Archive` (idempotent), arşivli kartta yeni işlem reddi
- [x] Domain testleri

### 3.3 Application (`FinTrack.Modules.CreditCards.Application`)

- [x] `CardDto`, `CardBalanceDto` (breakdown dahil) + mapper
- [x] Command/query: oluştur, güncelle (`rowVersion`), arşivle, listele
      (`status` filtresi), detay, bakiye
- [x] Port: `ICardRepository`, `ICardQueries`, `IUnitOfWork`, `ICardExpenseQuery`
      (ModuleContracts)
- [x] Handler testleri (SQLite in-memory, Expenses test altyapısıyla aynı desen)

### 3.4 Infrastructure (`FinTrack.Modules.CreditCards.Infrastructure`)

- [x] `CreditCardsDbContext`, `cards` şeması, EF config, `decimal(19,4)`, `rowversion`
- [x] Migration `InitialCreditCards` + LocalDB'de doğrulama
- [x] `ICardQuery` implementasyonu (Expenses doğrulaması için)
- [x] DI kaydı, design-time factory, `InternalsVisibleTo`

### 3.5 Endpoints (`FinTrack.Modules.CreditCards.Endpoints`)

- [x] `GET/POST /cards`, `GET/PUT /cards/{id}`, `POST /cards/{id}/archive`,
      `GET /cards/{id}/balance`
- [x] `CreditCardsProblems` (Error → ProblemDetails)
- [x] `RequireAuthorization`, `_status` kaldır, smoke testten çıkar

### 3.6 Açık sorular (yanıtlandı)

1. `ICardExpenseQuery` yalnızca tüm zamanlar toplamı döner; `asOf` slice 4'e ertelendi.
2. Arşivli kart için `GET /cards/{id}/balance` çalışır; geçmiş korunur.
3. `GET /cards` varsayılanı yalnızca `Active`; `?status=Archived` arşivlileri listeler.
4. Bilinmeyen alan reddi yalnızca kart uç noktalarında
   (`[JsonUnmappedMemberHandling(Disallow)]`); global JSON davranışı değişmez.
   Gövde bağlama hataları `ThrowOnBadRequest = false` ile her ortamda `400` döner.

### 3.7 Tests

- [x] `tests/FinTrack.Modules.CreditCards.Tests` (yeni proje, solution'a eklendi)
- [x] Entegrasyon: kart CRUD + arşiv + bakiye + kullanıcı izolasyonu
- [x] Regresyon: kart alışverişi aylık giderde 1, bakiyede 1; iade ikisini azaltır
- [x] Arşivli karta yeni harcama reddi (`POST /expenses` + `cardId`)
- [x] Mimari test: modüller arası yalnızca `ModuleContracts`

### 3.8 Doküman ve kapanış

- [x] `docs/07` slice 3 → ✅, test sayıları, changelog
- [x] `docs/06` §6 uygulama notları (sapma varsa)
- [x] `README.md` mevcut durum + doküman tablosu
- [x] `docs/08` §2–§6, `docs/09` kutuları
- [x] Dilim raporu

---

## 4. Slice 4 — Ekstre + kart ödemesi ✅ (2026-11-16)

### 4.1 Planlama

- [x] `docs/05` §5.3–5.6, `docs/06` §6.3–6.6, `docs/07` §4, `docs/08` §7 okundu
- [x] `docs/answers-1.txt` kararları bağlayıcı belgelere uygulandı
      (`docs/05`, `docs/06`, `docs/07`, `docs/08`)
- [x] 21 test senaryosu hazır (tarih kesiti, iptal, iade, `asOf`)
- [x] Test senaryoları onaylandı; gelecek `asOf` için `400`
      kararı ve iptal UTC→yerel tarih kuralı dokümanlara işlendi
- [x] Nihai doküman/senaryo kontrolü ve Slice 4 kodlama onayı
- [x] Sıra: Domain → Application → Infrastructure → Endpoints → tests

### 4.2 Kapsam (bağlayıcı: `docs/07` §4)

- [x] `CardPayment`: oluşturma, listeleme, iptal (`CancellationReason` zorunlu)
- [x] `Statement`: dönem/borç manuel; türetilen alanlar okuma anında
- [x] Devreden borç: önceki **aktif** ekstre; `(önceki.PeriodEnd, mevcut.PeriodEnd]`
- [x] Dönem çakışması yalnızca `Active` ekstrelerle
- [x] Bakiye formülüne `− payments`; `GET /cards/{id}/balance?asOf=YYYY-MM-DD`
- [x] `ICardExpenseQuery`'ye `asOf` (kullanıcı saat dilimi, gün sonu)
- [x] Migration + uç noktalar + modül/entegrasyon testleri

### 4.3 Doğrulama

- [x] Domain testleri: `CardPayment`, `Statement`, `UserTime` kesiti
- [x] Application testleri: ödeme, ekstre türetimi, devreden borç, iptal
- [x] Expenses: `GetCardTotalsAsOfAsync` gelecek hareket, iade, aynı gün iptal,
      UTC+14/UTC-11
- [x] Entegrasyon: ödeme CRUD/iptal/arşiv/izolasyon, ekstre türetimi/çakışma,
      bakiye `asOf` geçmiş/gelecek/format
- [x] Build 0 hata/0 uyarı; birim testleri 137/137
- [x] Entegrasyon testleri (46) — CI'da yeşil; güncel toplam 50 (Slice 5 dahil)

---

## 5. Slice 5 — Para girişi ✅ (2026-11-16)

### 5.1 Planlama

- [x] `docs/05` §4, `docs/06` §5, `docs/07` §4 Slice 5 okundu
- [x] Gelir niteliği türetimi, `Unknown` grubu ve borç kaynağı reddi netleşti
- [x] Borç bağlantısı için `DebtLinkId` + DTO `debtLink` kararı verildi
- [x] İptal/güncelleme kuralları ve hata eşlemesi belirlendi

### 5.2 Kapsam

- [x] `CashInflow` aggregate: tutar, para birimi, `occurredOn`, kaynak,
      açıklama, iptal, `rowVersion`
- [x] `IsEarnedIncome` kaynaktan türetilir; istemci gönderirse `400`
- [x] `Unknown` → `null`, gelire katılmaz; `Salary`/`BusinessIncome` → `true`
- [x] `BankLoan`, `CashAdvance`, `PersonalBorrowing` doğrudan oluşturma `400`
- [x] `CreateFromDebtOwner(...)` ve `LinkToDebt(...)` (slice 6/7 hazırlığı)
- [x] `inflows` şeması, DbContext, global query filter, migration
- [x] `GET/POST /cash-inflows`, `GET/PUT /cash-inflows/{id}`,
      `POST /cash-inflows/{id}/cancel`
- [x] Liste filtreleri + sayfalama; kullanıcı izolasyonu
- [x] `_status` uç noktası ve smoke test vakası kaldırıldı

### 5.3 Doğrulama

- [x] Domain + Application testleri: kaynak/gelir tablosu, doğrulama, iptal,
      borç bağlantısı, oluşturma/güncelleme, filtre/sayfalama, izolasyon
      (`tests/FinTrack.Modules.CashInflows.Tests`, 48 test)
- [x] Entegrasyon: yaşam döngüsü, türetim/borç reddi, filtre, izolasyon,
      OpenAPI (5 test)
- [x] Build 0 hata/0 uyarı (Debug + Release)
- [x] Birim 185/185, entegrasyon 50/50; toplam 235/235
- [x] Migration `20261116182933_InitialCashInflows`; LocalDB'de uygulandı,
      `has-pending-model-changes` → "No changes"

### 5.4 Doküman ve kapanış

- [x] `docs/07` Slice 5 → ✅, test sayıları, changelog
- [x] `docs/08` durum, doğrulama, kararlar, tuzaklar, changelog
- [x] `README.md` mevcut durum + doküman tablosu
- [x] `docs/09` kutular ve sıradaki dilim
- [x] Dilim raporu

---

## 6. Dilim durumu ve sonraki dilimler

### Slice 6 — Nakit avans ✅ (2026-11-17)

- [x] **ADR-0009 kabul edildi:** sync writer + paylaşılan `DbTransaction`;
      ambient `TransactionScope` ve outbox reddedildi.
- [x] **Domain:** `CashAdvance`, `CashAdvanceStatus`, domain hataları.
- [x] **Application:** create/cancel handler'ları, repository/sorgu portları,
      `CardDebtService` bakiye formülüne `+ cashAdvances`.
- [x] **Infrastructure:** `cards.CashAdvances`, EF config, repository/sorgu,
      migration `20261117174437_AddCashAdvances`.
- [x] **Endpoints:** `GET/POST /cards/{id}/cash-advances` ve iptal;
      `docs/06` §6.5 ile uyum.
- [x] **Atomiklik:** avans + bağlı `CashInflow` aynı transaction; iptalde ikisi
      birlikte kapanır; kart ödemeleri geri alınmaz.
- [x] **Testler:** 21 yeni modül + 5 yeni API entegrasyon testi; toplam
      261/261 (206 birim/mimari + 55 entegrasyon).
- [x] **CI:** run `44141605019` başarılı (Ubuntu + Testcontainers MSSQL).
- [x] **Dokümanlar:** `docs/07` v1.11, `docs/08` v2.1, `README.md`, ADR-0009.

### Slice 7 — Kredi + kişisel borç ✅ (2026-11-17)

- [x] **Domain:** `Loan`, `Installment` (tam vade planı), `InstallmentPayment`,
      `PersonalDebt`, `PersonalDebtRepayment`, durumlar ve domain hataları.
- [x] **Application:** create/get/list/cancel handler'ları, taksit ödeme/iptal,
      geri ödeme ekleme/iptal, repository/sorgu portları.
- [x] **Infrastructure:** `debts` şeması, EF config, repository/sorgu, global
      query filter, migration `20261117180809_InitialDebts`.
- [x] **Endpoints:** `GET/POST /loans`, `GET/POST /personal-debts`, taksit ve
      geri ödeme alt kaynakları; `docs/06` §7 ile uyum; Debts `_status` kaldırıldı.
- [x] **Atomiklik:** ADR-0009 ile kredi/borç ve bağlı `CashInflow` aynı
      transaction'da oluşturulur/iptal edilir; rollback testleri yeşil.
- [x] **Kurallar:** 31'e ay sonu, eşit taksit, sıralı ödeme, her ödenmiş taksitin
      iptali, kalan borç formülleri, türetilmiş alan reddi, kullanıcı izolasyonu.
- [x] **Testler:** 31 yeni modül + 8 yeni API entegrasyon testi; toplam
      300/300 (237 birim/mimari + 63 entegrasyon), CI run `44150709461` yeşil.
- [x] **CI:** run `44150709461` başarılı (Ubuntu + Testcontainers MSSQL).
- [x] **Dokümanlar:** `docs/05` §6, `docs/06` §7, `docs/07` v1.12, `docs/08`
      v2.2, `README.md`, bu dosya.

### Slice 8 — Raporlama ✅ (2026-11-17)

- [x] **ModuleContracts:** `ExpenseReportTotals`, `CashInflowReportTotals`,
      `CardDebtReport`/`CardStatementDue`, `LoanDebtEntry`, `PersonalDebtEntry`,
      `LoanInstallmentDue` ve 4 okuma sözleşmesi; mevcut 4 sözleşme korundu.
- [x] **Owner adapter'ları:** Expenses, CashInflows, CreditCards ve Debts
      **Application** katmanında; Infrastructure/DbContext değişmedi.
- [x] **Reporting.Application:** DTO/query/handler'lar + açık DI kayıtları;
      Reporting.Infrastructure boş (bilinçli: DbContext/migration yok).
- [x] **Endpoints:** `GET /reports/monthly`, `/debt-summary`,
      `/upcoming-payments`; `ReportingProblems` ile ProblemDetails; `_status`
      kaldırıldı, grup `RequireAuthorization()`.
- [x] **Kurallar:** takvim ayı, `Europe/Istanbul` varsayılan, kullanıcı saat
      dilimi; iade `RefundedOn` döneminde negatif gider; borç formülünde nakit
      avans bir kez, ekstre eklenmez, kart alacağı ayrı; yaklaşan ödemeler
      yeni kayıt oluşturmaz.
- [x] **Testler:** 10 yeni modül + 7 yeni API entegrasyon testi; toplam
      320/320 (248 birim/mimari + 72 entegrasyon), CI run `44156632893` yeşil.
- [x] **Mimari test:** Reporting başka modül katmanlarına/EF'e doğrudan
      bağımlı olamaz; `FinTrack.ModuleContracts` referansı zorunlu.
- [x] **Dokümanlar:** `docs/05` §7 ve §9/13–16, `docs/07` v1.13, `docs/08`
      v2.3, `README.md`, bu dosya.

### Slice 9 — MAUI istemci ✅ (2026-11-18)

- [x] **İş yükü:** `dotnet workload install maui-android maui-windows` başarılı
      (`10.0.110/10.0.100`); hedefler Android + Windows.
- [x] **Projeler:** `FinTrack.Mobile.Core` (net10.0), `FinTrack.Mobile`
      (Android + Windows), `FinTrack.Mobile.Tests` (xUnit); çözümde 33 proje.
- [x] **İstemci çekirdeği:** typed `HttpClient`, `ApiJson`, `docs/06` DTO'ları,
      `ApiException`, `AuthHandler` + `SessionRefresher` (401 refresh, ayrı
      HttpClient, `SemaphoreSlim` single-flight), `SessionService`,
      `ITokenStore`, `IAppNavigator`.
- [x] **Güvenlik:** `SecureStorage` token store; token loglanmaz/hata mesajına
      eklenmez; `Idempotency-Key` olmadığı için POST/PUT otomatik retry yok,
      retry yalnızca GET/HEAD/OPTIONS.
- [x] **UI:** Shell + MVVM (`CommunityToolkit.Mvvm`) ve 10 ekran: giriş/kayıt,
      özet, harcamalar + ekleme, kartlar + kart detayı, borçlar, raporlar,
      ayarlar (saat dilimi + çıkış).
- [x] **.NET 10 uyumu:** `IQueryAttributable.ApplyQueryAttributes`,
      `CreateWindow`, `DisplayAlertAsync`; Release `x:DataType` uyarıları
      temizlendi (0/0).
- [x] **Testler:** 15 mobile testi; toplam 263/263 birim/mimari (248 backend +
      15 mobile) + 72 entegrasyon = **335/335** beklenen; Debug + Release yeşil.
- [x] **Build:** Windows hedefi Debug + Release 0 uyarı/0 hata; backend Release
      0/0.
- [x] **Android/CI:** yerel SDK/JDK eksik → Android derlemesi CI Ubuntu
      job'ında (Java 17 + `dotnet workload restore`) derlendi; run
      `44189257561` yeşil (335/335).
- [ ] **Emülatör smoke:** Android emülatörü kurulmadı; manuel uçtan uca akış
      daha sonra.
- [x] **Dokümanlar:** `docs/07` v1.14, `docs/08` v2.4, `README.md`, bu dosya.

### Yüksek seviye

- [x] **Slice 4 — Ekstre + kart ödemesi:** ✅ 2026-11-16. `CardPayment`,
      `Statement`, devreden borç, `computedDebtAtPeriodEnd`, mutabakat farkı;
      borç formülüne ödeme terimi. Ayrıntı §4.
- [x] **Slice 5 — Para girişi:** ✅ 2026-11-16. `CashInflow`, kaynak türleri,
      `IsEarnedIncome` türetimi, `Unknown` grubu; borç doğuran girişlerin reddi.
      Ayrıntı §5.
- [x] **Slice 6 — Nakit avans:** ✅ 2026-11-17. ADR-0009 ile atomik
      `CashAdvance` + bağlı `CashInflow`, iptalde ikisinin birlikte iptali,
      borç formülüne avans terimi. Ayrıntı yukarıda ve `docs/08` §7.3.
- [x] **Slice 7 — Kredi + kişisel borç:** ✅ 2026-11-17. Tam vade planı, taksit
      ödemesi/iptali, kalan borç, geri ödeme ve ADR-0009 atomikliği. Ayrıntı
      yukarıda ve `docs/08` §7.4.
- [x] **Slice 8 — Raporlama:** ✅ 2026-11-17. Aylık özet, kategori kırılımı,
      borç özeti, yaklaşan ödemeler; `Europe/Istanbul` dönem sınırları. Ayrıntı
      yukarıda ve `docs/08` §7.5.
- [x] **Slice 9 — MAUI istemci:** ✅ 2026-11-18. Shell + MVVM, typed
      `HttpClient`, SecureStorage + 401 refresh, v1 ekranları; Android CI
      doğrulaması ve emülatör smoke'u kaldı. Ayrıntı yukarıda.

### Teknik borç / ertelenen

- [ ] `Idempotency-Key` (tüm POST) — ayrı görev
- [x] CI bakımı — action'lar Node 24 major sürümlerine yükseltildi
      (`checkout@v7`, `setup-dotnet@v6`, `cache@v6`, `setup-java@v6`,
      `upload-artifact@v7`); runner `ubuntu-24.04` sabitlendi; `actionlint` 0 hata
- [x] Sürüm kontrolü (git) — başlatıldı
- [x] CI — GitHub Actions (`.github/workflows/ci.yml`); uzak depo
      [`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack),
      Slice 8 commit'i sonrası yeşil (320/320, run `44156632893`, Ubuntu +
      Testcontainers MSSQL)
- [x] `docs/01` ayrı dosya gerekli değil
- [x] Yerel entegrasyon testleri Docker Desktop açıkken çalıştırıldı (50/50,
      2026-11-16); Slice 6 sırasında daemon kapalıydı, 5 yeni test CI'da
      doğrulandı

---

## 7. Dosya haritası (tekrar okumayı önlemek için)

| İhtiyaç | Bakılacak yer |
|---|---|
| Auth/JWT deseni | `src/Host/FinTrack.Api/Identity/` |
| Modül kompozisyonu | `src/Modules/Expenses/FinTrack.Modules.Expenses.Endpoints/ExpensesModule.cs` |
| Handler + transaction deseni | `...Expenses.Application/Expenses/ExpenseHandlers.cs`, `RefundHandlers.cs` |
| EF config / migration deseni | `...Expenses.Infrastructure/` (`Configurations`, `Migrations`, `ExpensesDbContext.cs`) |
| Query projeksiyon deseni | `...Expenses.Infrastructure/Queries/Queries.cs` |
| ProblemDetails eşlemesi | `...Expenses.Endpoints/ExpensesProblems.cs`, `RequestParsing.cs` |
| SQLite test altyapısı | `tests/FinTrack.Modules.Expenses.Tests/Support/` |
| Para girişi modülü | `src/Modules/CashInflows/`, `tests/FinTrack.Modules.CashInflows.Tests/` |
| Testcontainers fixture | `tests/FinTrack.Api.IntegrationTests/ApiFixture.cs` |
| Mimari sınırlar | `tests/FinTrack.ArchitectureTests/` |
| Ortak tipler | `src/BuildingBlocks/FinTrack.SharedKernel/` |
| Modüller arası sözleşmeler | `src/BuildingBlocks/FinTrack.ModuleContracts/` |
| ADR-0009 atomik yazma | `src/BuildingBlocks/FinTrack.ModuleContracts/AtomicWrites.cs`, `src/Host/FinTrack.Api/WriteTransactions/` |
| Nakit avans modülü | `src/Modules/CreditCards/…/CashAdvances/`, `CashAdvanceEndpoints.cs`, `docs/adr/0009-moduller-arasi-atomik-yazma.md` |
| Borç modülü | `src/Modules/Debts/`, `tests/FinTrack.Modules.Debts.Tests/` |
| Raporlama modülü | `src/Modules/Reporting/…/Application/Reporting/`, `src/Modules/Reporting/…/Endpoints/`, `tests/FinTrack.Modules.Reporting.Tests/` |
| Rapor okuma sözleşmeleri | `src/BuildingBlocks/FinTrack.ModuleContracts/ReportingReportContracts.cs` |
| Rapor adapter'ları | `src/Modules/{Expenses,CashInflows,CreditCards,Debts}/…/Application/Reporting/` |
| Mobil istemci | `src/Mobile/FinTrack.Mobile.Core/`, `src/Mobile/FinTrack.Mobile/`, `tests/FinTrack.Mobile.Tests/` |
| Token refresh akışı | `src/Mobile/FinTrack.Mobile.Core/Api/AuthHandler.cs`, `SessionRefresher.cs` |
| Shell / rotalar | `src/Mobile/FinTrack.Mobile/AppShell.xaml`, `MauiProgram.cs` |

---

## 8. Değişiklik geçmişi

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-11-14 | İlk sürüm; oturum protokolü, slice 3 görev kırılımı, açık sorular |
| 1.1 | 2026-11-14 | Slice 3 tamamlandı; kutular işaretlendi, açık sorular yanıtlandı, sıradaki dilim slice 4 |
| 1.2 | 2026-11-14 | Sürüm kontrolü başlatıldı; `docs/01` kapatıldı; GitHub Actions CI eklendi |
| 1.3 | 2026-11-14 | Release build + 139/139 doğrulandı; Docker ön koşulu not edildi |
| 1.4 | 2026-11-14 | Entegrasyon fixture'ı env var'a geçti; testler gerçek konteynere bağlanıyor |
| 1.5 | 2026-11-14 | GitHub uzak depo + ilk yeşil CI çalıştırması işaretlendi |
| 1.6 | 2026-11-15 | Slice 4 planlama bölümü eklendi; 21 test senaryosu onayı bekliyor olarak işaretlendi |
| 1.7 | 2026-11-15 | Gelecek `asOf` `400` ve iptal UTC→yerel tarih kuralı işlendi; nihai kontrol ve açık kodlama onayı bekleniyor |
| 1.8 | 2026-11-16 | Slice 4 tamamlandı ve kutular işaretlendi; 137 birim testi yeşil, 46 entegrasyon Docker kapalı olduğu için CI'ya bırakıldı; sıradaki dilim Slice 5 |
| 1.9 | 2026-11-16 | Slice 5 tamamlandı ve kutular işaretlendi; 185 birim + 50 entegrasyon = 235 test yerelde yeşil; aktif dilim Slice 6 (ADR-0009 ön koşulu) |
| 1.10 | 2026-11-17 | ADR-0009 kabul edildi; Slice 6 tamamlandı; 21 modül + 5 entegrasyon testi eklendi, toplam 261/261, CI run `44141605019` yeşil, migration senkron; aktif dilim Slice 7 |
| 1.11 | 2026-11-17 | Slice 7 tamamlandı; 31 modül + 8 entegrasyon testi eklendi, toplam 300/300, CI run `44150709461` yeşil, migration senkron; aktif dilim Slice 8 |
| 1.12 | 2026-11-17 | Slice 8 tamamlandı; 10 modül + 7 entegrasyon testi eklendi, toplam 320/320, CI run `44156632893` yeşil; Reporting salt okunur, DbContext/migration yok, `_status` kaldırıldı; aktif dilim Slice 9 |
| 1.13 | 2026-11-18 | Slice 9 tamamlandı; MAUI iş yükleri kuruldu, 3 proje + 15 test eklendi, toplam 263 birim/mimari + 72 entegrasyon = 335; Windows Debug/Release 0/0; Android CI run `44189257561` yeşil; emülatör smoke'u kaldı |
| 1.14 | 2026-11-18 | CI bakımı: GitHub action'ları Node 24 major sürümlerine yükseltildi, runner `ubuntu-24.04` sabitlendi, `actionlint` ile workflow doğrulandı |
| 1.15 | 2026-11-18 | CI bakım run'ı `44365407221` yeşil (335/335, `ubuntu-24.04`) son doğrulama olarak işlendi |
