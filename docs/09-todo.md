# FinTrack — Görev Listesi (TODO)

**Sürüm:** 1.8
**Tarih:** 2026-11-16
**Bağlı belgeler:** [Durum notu v1.9](08-durum-ve-devam-notu.md) · [Yol haritası v1.9](07-yol-haritasi-ve-ajan-gorev-standardi.md)

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
4. Gereken tek tek dosyalar için §6'daki haritayı kullan (modül kopyalama yok).
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
| Aktif dilim | **5 — Para girişi** (planlama) |
| Son doğrulama | Build 0/0 · 137/137 birim testi · 46 entegrasyon Docker kapalı (CI'da) · 2026-11-16 |
| Sıradaki komut | `docs/05` §4 ve `docs/06` §5'i oku; Slice 5 `CashInflow` kapsamını planla |

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
- [ ] Entegrasyon testleri (46) — Docker motoru kapalı; CI'da çalışacak

---

## 5. Sonraki dilimler (yüksek seviye)

- [x] **Slice 4 — Ekstre + kart ödemesi:** ✅ 2026-11-16. `CardPayment`,
      `Statement`, devreden borç, `computedDebtAtPeriodEnd`, mutabakat farkı;
      borç formülüne ödeme terimi. Ayrıntı §4.
- [ ] **Slice 5 — Para girişi:** sıradaki dilim. `CashInflow`, kaynak türleri, `IsEarnedIncome`
      türetimi, `Unknown` grubu; borç doğuran girişler.
- [ ] **Slice 6 — Nakit avans:** `CashAdvance` + bağlı `CashInflow`, iptalde
      ikisinin birlikte iptali, borç formülüne avans terimi.
- [ ] **Slice 7 — Kredi + kişisel borç:** tam vade planı, taksit ödemesi,
      kalan borç; borç doğuran giriş zorunluluğu.
- [ ] **Slice 8 — Raporlama:** aylık özet, kategori kırılımı, borç özeti,
      yaklaşan ödemeler; `Europe/Istanbul` dönem sınırları.
- [ ] **Slice 9 — MAUI istemci:** Shell + MVVM, typed `HttpClient`, auth akışı.

### Teknik borç / ertelenen

- [ ] `Idempotency-Key` (tüm POST) — ayrı görev
- [x] Sürüm kontrolü (git) — başlatıldı
- [x] CI — GitHub Actions (`.github/workflows/ci.yml`); uzak depo
      [`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack),
      ilk çalıştırma yeşil (139/139, Ubuntu + Testcontainers MSSQL)
- [x] `docs/01` ayrı dosya gerekli değil
- [ ] Yerel entegrasyon testleri için Docker Desktop açık olmalı (aksi halde
      46 `DockerUnavailableException`)

---

## 6. Dosya haritası (tekrar okumayı önlemek için)

| İhtiyaç | Bakılacak yer |
|---|---|
| Auth/JWT deseni | `src/Host/FinTrack.Api/Identity/` |
| Modül kompozisyonu | `src/Modules/Expenses/FinTrack.Modules.Expenses.Endpoints/ExpensesModule.cs` |
| Handler + transaction deseni | `...Expenses.Application/Expenses/ExpenseHandlers.cs`, `RefundHandlers.cs` |
| EF config / migration deseni | `...Expenses.Infrastructure/` (`Configurations`, `Migrations`, `ExpensesDbContext.cs`) |
| Query projeksiyon deseni | `...Expenses.Infrastructure/Queries/Queries.cs` |
| ProblemDetails eşlemesi | `...Expenses.Endpoints/ExpensesProblems.cs`, `RequestParsing.cs` |
| SQLite test altyapısı | `tests/FinTrack.Modules.Expenses.Tests/Support/` |
| Testcontainers fixture | `tests/FinTrack.Api.IntegrationTests/ApiFixture.cs` |
| Mimari sınırlar | `tests/FinTrack.ArchitectureTests/` |
| Ortak tipler | `src/BuildingBlocks/FinTrack.SharedKernel/` |
| Modüller arası sözleşmeler | `src/BuildingBlocks/FinTrack.ModuleContracts/` |

---

## 7. Değişiklik geçmişi

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
