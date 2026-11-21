# FinTrack — Durum ve Devam Notu

**Sürüm:** 2.19
**Tarih:** 2026-11-21
**Bağlı belgeler:** [Yol haritası v1.18](07-yol-haritasi-ve-ajan-gorev-standardi.md) · [API Sözleşmeleri v1.7](06-api-sozlesmeleri.md) · [Teknik Mimari v1.3](03-mimari.md) · [İş Kuralları v1.6](05-is-kurallari.md) · [Kararlar v1.2](10-karar-bekleyen-konular.md) · [Idempotency teknik tasarımı v1.5](11-idempotency-teknik-tasarim.md) · [Görev listesi v1.29](09-todo.md) · ADR-0001…0010
**Hedef okuyucu:** geliştirici ve geliştirme ajanları

> Bu dosya **tek doğruluk kaynağı değildir**; hızlı devam notudur. Bağlayıcı olan
> `docs/05`, `docs/06` ve `docs/07`'dir. Çelişki görülürse bu dosya düzeltilir.
>
> **Güncelleme kuralı:** Her dilim bittiğinde §2, §3, §4 ve §9 güncellenir.
> Yeni oturum başında önce bu dosya, sonra `docs/07` §4 (ilgili dilim) okunur.

---

## 1. Nasıl kullanılır

1. `docs/09` görev listesinden sıradaki dilimi ve açık maddeleri öğren.
2. Bu dosyadan güncel kararları, tuzakları ve son doğrulamayı oku.
3. İlgili dilimin bağlayıcı tanımını `docs/07` §4'ten oku.
4. Açık soru varsa **kod yazmadan** sor (§7).
5. Dilim bitince: build + test yeşil, dokümanlar güncel, bu dosya ve `docs/09` güncel.

---

## 2. Güncel durum

| # | Dilim | Durum |
|---|---|---|
| 0 | İskelet | ✅ |
| 1 | Kimlik doğrulama | ✅ |
| 2 | Harcama + kategori | ✅ |
| 3 | Kart + hesaplanan bakiye | ✅ |
| 4 | Ekstre + kart ödemesi | ✅ |
| 5 | Para girişi | ✅ |
| 6 | Nakit avans | ✅ |
| 7 | Kredi + kişisel borç | ✅ |
| 8 | Raporlama | ✅ |
| 9 | MAUI istemci | ✅ |

**Çözüm:** `FinTrack.slnx`, **33 proje** (Slice 8 ile Reporting test, Slice 9 ile
`FinTrack.Mobile.Core`, `FinTrack.Mobile`, `FinTrack.Mobile.Tests` eklendi). Depo
**git** (`main`); uzak depo
[`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack) (public).
GitHub Actions CI (`.github/workflows/ci.yml`) Slice 9 build + testlerini
doğruladı: run `44189257561` (Slice 9), `44365407221` (CI bakımı),
`44366617692` (dokümanlar) ve `44379256250` (Android düzeltmeleri) yeşil
(335/335; Android + Windows hedefleri; Slice 8 run `44156632893`, Slice 7 run
`44150709461` idi). Android emülatör smoke'u giriş → harcama → kart → rapor
akışıyla tamamlandı.

**v1.1 durumu (2026-11-19):** Açık kararlar `docs/10` v1.2 ile kapatıldı;
`docs/11-kararlar.txt` ile **altı koşullu tasarım onayı** alındı ve `docs/11`
v1.3 + **ADR-0010**'da işlendi. Görev kartı `docs/07` §5.1'de (V11-T01).
Kullanıcı açık kodlama onayını verdi; **V11-T01 uygulandı ve 435/435 test
yeşil** (`docs/11` §9.5). Dört kritik konunun çözümü `docs/11` §9.2'de; kalan
riskler §9.4'te. Karar dokümanı push'u `decd69d`, docs-only CI run'ı
`44385538919`; kod doğrulaması için `44379256250` referans alınır (karar 2.4
gereği son docs run'ı eklenmez). **Kod commit'i `5e614c7` push edildi**
(2026-11-19); push sonrası CI sonucu ve operasyonel maddeler §8.5'te.
**Operasyonel:** LocalDB smoke verileri temizlendi (0 satır doğrulandı); Windows
manuel UI smoke operatör tarafından tamamlanacak (§8.4, §8.3 madde 2).

**V11-T02 (2026-11-20):** Gerçek `AddExpenseViewModel.SaveAsync` akışı
PendingOperation altyapısına bağlandı: doğrulama → tek anahtar → sabit gövde →
`Created` kaydı → `SentUnconfirmed` → aynı key/body ile POST → başarıda silme /
belirsizlikte `Uncertain` veya pending. Startup recovery oturum hazır olduktan
sonra tek uçuş olarak tetikleniyor; kullanıcı anahtarı JWT `sub`'den türetiliyor.
`FinTrack.Mobile.Tests` **45/45** (14 yeni entegrasyon testi); çözüm build'i
JDK 17 ile 0 uyarı/0 hata. Ayrıntılar §8.7 ve `docs/11` §9.6.

**V12-T02 (2026-11-21):** Transaction Participant Audit kapanışı için
`IdempotencyMiddleware`'da ölü `committed` state'i kaldırıldı; 2xx ve
expired→Tombstone commit yolları `CommitStatusChangeAsync` altında toplandı.
S25'in adı dört ayrı POST/anahtar/transaction gerçeğiyle düzeltildi;
expired→Tombstone tek-commit ve gerçek middleware invocation ile kapsam dışı
yol testleri eklendi. Audit F-03/F-04/F-05/F-11 ve F-13 (yorum) kapatıldı;
F-09/F-10/F-12/F-14 bu turda kapatılmadı. Docker erişilebilir olduğu için
entegrasyonlar gerçek Docker/Testcontainers + MSSQL ile **95/95** geçti;
Docker hariç testler **390/390**, build 0/0. Beş kod/test dosyası
commit/push bekliyor, yeni CI yok, son yeşil CI `44931169879` (`1826dc7`).
Ayrıntılar §8.11, `docs/11` §9.7 ve audit raporu §10.

**Mobil:** `src/Mobile/FinTrack.Mobile.Core` (platform bağımsız net10.0: DTO,
typed `HttpClient`, `AuthHandler`/`SessionRefresher`, `SessionService`, 10
ViewModel), `src/Mobile/FinTrack.Mobile` (MAUI Shell + v1 XAML ekranları,
`SecureStorageTokenStore`, `MauiNavigator`), `tests/FinTrack.Mobile.Tests`
(45 xUnit testi). Hedefler: Android + Windows.

**Yazılmış modüller:** Identity (`FinTrack.Api/Identity`, `identity` şeması),
Expenses (`expenses` şeması, migration, uç noktalar), CreditCards (`cards` şeması,
migration, kart/ekstre/ödeme/nakit avans uç noktaları), CashInflows (`inflows`
şeması, migration, para girişi uç noktaları), Debts (`debts` şeması, migration,
kredi/taksit/kişisel borç uç noktaları), Reporting (salt okunur aylık özet + borç
özeti + yaklaşan ödemeler; DbContext/migration yok, veri yalnızca
`ModuleContracts` üzerinden). Tüm modüllerde `_status` kaldırıldı; route grupları
`RequireAuthorization()` kullanır.

---

## 3. Son doğrulama

**Tarih:** 2026-11-18 · **SDK:** `10.0.404` · **MAUI iş yükü:** `10.0.110/10.0.100` · **Microsoft paketleri:** `10.0.15`

> **2026-11-18 notu:** Slice 9 tamamlandı. Backend Debug/Release build 0/0;
> Windows MAUI build (Debug + Release) 0/0; birim/mimari **263/263** (248
> backend + 15 mobile). Android SDK/JDK kuruldu; `net10.0-android` yerel JDK 17
> ile 0 uyarı/0 hata derlendi ve `fintrack_smoke` AVD'sinde giriş → harcama →
> kart → rapor smoke'u başarılı. Entegrasyon 72/72; toplam **335/335** yerelde
> (Docker açık) ve CI'da doğrulandı. CI bakımı sonrası run `44365407221`,
> dokümanlar için `44366617692` ve Android düzeltmeleri için `44379256250`
> yeşil (335/335, `ubuntu-24.04`).

| Kontrol | Komut | Sonuç |
|---|---|---|
| Build (Debug, backend) | `dotnet build FinTrack.slnx --no-restore` (Slice 8 sonu) | ✅ 0 uyarı, 0 hata |
| Build (Windows MAUI, Debug) | `dotnet build src/Mobile/FinTrack.Mobile -f net10.0-windows10.0.19041.0` | ✅ 0 uyarı, 0 hata |
| Build (Release, backend) | `dotnet build src/Host/FinTrack.Api -c Release` | ✅ 0 uyarı, 0 hata |
| Build (Release, Windows MAUI) | `dotnet build src/Mobile/FinTrack.Mobile -c Release -f net10.0-windows10.0.19041.0` | ✅ 0 uyarı, 0 hata |
| Birim testleri (Debug) | 8 birim/mimari projesi ayrı ayrı | ✅ 263/263 |
| Release testleri | 8 birim/mimari projesi `-c Release` | ✅ 263/263 |
| Entegrasyon testleri | `dotnet test tests/FinTrack.Api.IntegrationTests` | ✅ 72/72 (yerel Docker açık; CI run `44156632893`) |
| Android derlemesi | `dotnet build src/Mobile/FinTrack.Mobile/FinTrack.Mobile.csproj -f net10.0-android -p:JavaSdkDirectory=<JDK 17>` | ✅ 0 uyarı, 0 hata (yerel JDK 17 + SDK 36; CI Ubuntu job'unda da yeşil) |
| Android uçtan uca smoke | `dotnet build ... -f net10.0-android -t:Install` + `fintrack_smoke` AVD | ✅ giriş → harcama (125,00 TRY / `Market`) → kartlar (`0 aktif kart`) → raporlar (`Kategori kırılımı: Market`) |
| CI | GitHub Actions `build-test` | ✅ run `44365407221` (CI bakımı), `44366617692` (dokümanlar) ve `44379256250` (Android düzeltmeleri) yeşil (335/335; `ubuntu-24.04`); önceki: `44189257561` (335/335; Android + Windows), `44156632893` (320/320) |
| CI workflow lint | `actionlint` v1.7.12 — `.github/workflows/ci.yml` | ✅ 0 hata (Node 24 action'ları + `ubuntu-24.04`) |
| Migration | `dotnet ef migrations has-pending-model-changes --context DebtsDbContext ...` | ✅ "No changes have been made to the model since the last migration." (Slice 9'da DbContext/entity/migration yok) |

**Test dağılımı (335 = 263 birim + 72 entegrasyon):**

| Proje | Adet | Kapsam |
|---|---|---|
| `FinTrack.ArchitectureTests` | 4 | Modül sınırı, katman bağımlılığı, Reporting sınırı |
| `FinTrack.Api.UnitTests` | 22 | Kimlik doğrulama, JWT |
| `FinTrack.Modules.Expenses.Tests` | 55 | Domain + handler (SQLite in-memory) |
| `FinTrack.Modules.CreditCards.Tests` | 78 | Domain + handler (SQLite in-memory; nakit avans dahil) |
| `FinTrack.Modules.CashInflows.Tests` | 48 | Domain + handler (SQLite in-memory) |
| `FinTrack.Modules.Debts.Tests` | 31 | Domain + handler + ADR-0009 atomiklik (SQLite in-memory) |
| `FinTrack.Modules.Reporting.Tests` | 10 | Handler, fake `ModuleContracts` |
| `FinTrack.Mobile.Tests` | 45 | `AuthHandler`, token akışı, ViewModel doğrulama/hata eşlemesi, V11-T02 ViewModel → PendingOperation → HTTP entegrasyonu |
| `FinTrack.Api.IntegrationTests` | 72 | 9 smoke + 17 kimlik + 10 harcama + 6 kart + 6 ekstre/ödeme + 5 para girişi + 4 nakit avans + 8 kredi/borç + 7 raporlama (Testcontainers MSSQL) |

**Uygulanan migration'lar:**
- `20261113215049_InitialExpenses` (`expenses.Categories`, `expenses.Expenses`,
  `expenses.Refunds`, `expenses.__EFMigrationsHistory`).
- `20261114100405_InitialCreditCards` (`cards.Cards`,
  `cards.__EFMigrationsHistory`; `IX_Cards_UserId_IsArchived`).
- `20261116180727_AddCardPaymentsAndStatements` (`cards.CardPayments`,
  `cards.Statements` ve kullanıcı/kart/dönem indeksleri).
- `20261116182933_InitialCashInflows` (`inflows.CashInflows`,
  `inflows.__EFMigrationsHistory`; kullanıcı/tarih/kaynak/durum indeksleri).
- `20261117174437_AddCashAdvances` (`cards.CashAdvances`, benzersiz
  `LinkedCashInflowId`, kullanıcı/kart/durum/tarih indeksleri; SQLite testlerinde
  8 baytlık `rowversion`).
- `20261117180809_InitialDebts` (`debts.Loans`, `debts.Installments`,
  `debts.InstallmentPayments`, `debts.PersonalDebts`,
  `debts.PersonalDebtRepayment`; `rowversion`, kullanıcı/durum/tarih ve aktif
  ödeme benzersizlik indeksleri).

---

## 4. Alınan kararlar

### 4.1 Mimari

| Karar | Kaynak |
|---|---|
| Modüler monolit; modül içi Clean Architecture; derleyiciyle korunan sınırlar | ADR-0001, ADR-0002 |
| Modül başına şema + DbContext; modüller arası erişim yalnızca `ModuleContracts` | ADR-0004 |
| CQRS, MediatR yok; `ICommandHandler` / `IQueryHandler` işaret arayüzleri | ADR-0003 |
| Kart alışverişi Expenses'a aittir; kart borcu hesaplanır, saklanmaz | ADR-0008, ADR-0004 |
| Ödemeler sahibi modülde; aynı olay iki kez sayılmaz | ADR-0007 |
| Modüller arası atomik yazma: sync writer + paylaşılan `DbTransaction` | ADR-0009 |
| Idempotent mutasyon POST: host sahipliğinde tek `DbConnection`/`DbTransaction`, tek commit, benzersiz key | ADR-0010, `docs/11` §4 (koşullu onay + açık kod onayı alındı; `5e614c7` ile uygulandı) |

### 4.2 API sözleşmesi

| Karar | Kaynak |
|---|---|
| Para JSON'da `number` / `decimal`; enum'lar `PascalCase` string | `docs/06` §1 |
| `DELETE` yok; iptal `POST /{id}/cancel`, arşiv `POST /{id}/archive` | `docs/06` §1 |
| Eşzamanlılık gövdedeki `rowVersion` ile; uyuşmazlık `409 ConcurrencyConflict` | `docs/06` §1.5 |
| Sayfalama `page=1`, `pageSize=50` (max 200); hatalı değer `400 ValidationFailed` | `docs/06` §1.6 |
| `Idempotency-Key` **V11-T01 ile uygulandı**; v1.1 sözleşmesi + koşullu tasarım onayı + açık kodlama onayı tamam (§6, §8.4, `docs/11` §9.5) | `docs/06` §1.4 (v1.7), `docs/10` v1.2, `docs/11` v1.3, ADR-0010 |

### 4.3 İş kuralları

| Karar | Kaynak |
|---|---|
| Ekstre alanları manuel; nakit avans kart borcuna dahil; faiz/ücret kullanıcı girer | `docs/02` §5 |
| Taksit planı tam vade; durum + iade/iptal kayıtları yeterli, tam audit log yok | `docs/02` §5 |
| Raporlama takvim ayı, `Europe/Istanbul` varsayılan | `docs/03`, `docs/02` §5 |
| `IsEarnedIncome` kaynaktan türetilir; kullanıcı seçemez | `docs/05` |
| Finansal kayıt hard delete edilmez; iptal/arşiv | `docs/05` |

### 4.4 Slice 1 (kimlik)

| Karar |
|---|
| Access token 15 dk; refresh 30 gün, DB'de SHA-256 hash, rotasyonlu |
| Dev'de geçici imza anahtarı; prod'da `Jwt__SigningKey` ≥ 32 bayt zorunlu |
| Migration yalnızca `--migrate` veya `Database:MigrateOnStartup=true` |
| Profil eşzamanlılığı SQL Server `rowversion`, base64 |

### 4.5 Slice 2 (harcama + kategori)

| Karar | Gerekçe |
|---|---|
| `Expense.Status` **kolon değil**, iadelerden hesaplanır | Tek doğruluk; iade iptali durumu otomatik düzeltir |
| Yalnızca `CancelledAtUtc` + `CancellationReason` saklanır | İptal gerekçesi zorunlu, ayrı durum alanı gereksiz |
| Kategori adı benzersizliği arşivliler dahil, büyük/küçük harf duyarsız | Aynı adla ikinci kategori kafa karıştırır |
| Normalizasyon `tr-TR` uppercase (`Gıda` → `GIDA`) | `ToUpperInvariant` Türkçe `i/ı` eşleşmesini bozar |
| Arşiv idempotent `204`; arşivli kategori meta verisi düzenlenebilir | Kullanıcı arşivi geri alamaz ama adını düzeltebilir |
| Arşivli kart/kategori yeni harcamada reddedilir; güncellemede mevcut değer korunabilir | Geçmiş kayıt bozulmaz |
| Bilinmeyen/arşivli kartta `400 ValidationFailed` (`cardId`) | `ICardQuery` sözleşmesi |
| Para birimi `TRY` (case-insensitive); tutar > 0, `decimal(19,4)` | v1 tek para birimi |
| İade ekleme/iptal ve tutar düşürme `Serializable` transaction | Eşzamanlı iadeler toplamı aşmasın |
| `recordedAtUtc` istemciden alınmaz; PUT'ta `400` | Sunucu zamanı |
| "Bugün" JWT `preferred_timezone` + `UserTime.Today`; 15 dk eski TZ kabul | Kullanıcı saat dilimi |
| Modül-lokal `ExpensesProblems`; yeni shared proje yok | Gereksiz soyutlama yok |

### 4.6 Slice 3 (kart + hesaplanan bakiye)

| Karar | Gerekçe |
|---|---|
| `CardStatus` kolon değil; `IsArchived` → `Active`/`Archived` türetilir | Tek doğruluk; durum alanı ile arşiv tarihi çelişemez |
| `GET /cards` varsayılanı yalnızca `Active`; `?status=Archived` arşivlileri listeler | Kullanıcı normalde aktif kartlarla çalışır |
| Arşivli kartın detayı ve geçmiş bakiyesi okunur; meta verisi düzenlenebilir | Geçmiş kayıt bozulmaz (docs/05 §5.1) |
| Arşivli karta yeni harcama/ödeme/avans reddedilir (`400`, `cardId`) | docs/05 §5.1–5.3 |
| `ICardExpenseQuery` tüm zamanlar toplamı döner; `asOf` slice 4'te eklendi | Slice 3 geriye uyumlu; kesitli toplam `GetCardTotalsAsOfAsync` |
| Slice 3'te bakiye `debt = expenses − refunds`; `payments` slice 4'te, `cashAdvances` slice 6'da dolar | Formül sözleşmede hazır |
| Yalnızca aktif harcama ve aktif iadeler sayılır | İptal edilen harcamanın iadesi de sayılmaz |
| Bilinmeyen alan reddi yalnızca kart isteklerinde (`JsonUnmappedMemberHandling.Disallow`) | Global JSON davranışı değişmez; kart no/CVV/SKT sızmasın |
| `RouteHandlerOptions.ThrowOnBadRequest = false` | Gövde hatası Development'ta 500 değil, her ortamda `400` |
| `recordedAtUtc` domain `CreatedAtUtc`'ten gelir; istemciden alınmaz | Sunucu zamanı |
| `creditLimit` opsiyonel, `> 0`, en fazla 4 ondalık; borca girmez | docs/06 §6.2 |
| `rowVersion` SQL Server `rowversion`, base64 | Mevcut eşzamanlılık deseni |

### 4.7 Slice 4 — ekstre + kart ödemesi (2026-11-15)

| Karar | Gerekçe |
|---|---|
| Türetilen ekstre alanları okuma anında hesaplanır, saklanmaz | Geçmiş tarihli iade/ödeme/iptal geçmiş ekstreyi güncelleyebilsin (docs/05 §5.6) |
| Dönem çakışmasını yalnızca `Active` ekstreler engeller | İptal edilmiş ekstre yeni dönemi bloke etmez |
| Kesit `DateOnly` + kullanıcı saat dilimi; gün sonu dahil | Ekstre ve bakiye aynı tarih semantiğini kullanır |
| İptal UTC zamanı kullanıcı yerel tarihine çevrilir; aynı yerel gün iptali kesitte iptal sayılır | Harcama, iade ve ödeme için tek kural |
| Gelecek `asOf` `400 ValidationFailed`; bugüne kırpılmaz | Geçmişe dönük kesit anlamlı; gelecek veri yok |
| `carriedOverAmount` aralığı `(önceki.PeriodEnd, mevcut.PeriodEnd]` | Sınır günleri ve çift sayım netleşti |
| `GET /cards/{id}/balance` opsiyonel `asOf`; yanıtta `asOfDate` | Geçmiş bakiye ve ekstre karşılaştırması |

### 4.8 Slice 5 — para girişi (2026-11-16)

| Karar | Gerekçe |
|---|---|
| `IsEarnedIncome` kolon değil; `CashInflow.EarnedIncomeFor(source)` ile türetilir | Tek doğruluk; istemci alanı gönderemez (`400 ValidationFailed`) |
| `Unknown` → `null`; `Salary`/`BusinessIncome` → `true`; diğerleri `false` | docs/05 §4.2 tablosu; `Unknown` gelir toplamlarına katılmaz |
| `BankLoan`, `CashAdvance`, `PersonalBorrowing` doğrudan `POST /cash-inflows` ile oluşturulamaz | Sahibi modül atomik oluşturur; aksi halde `400 DebtSourceRequiresOwnerEndpoint` |
| DB'de yalnızca nullable `DebtLinkId`; DTO `debtLink.type` kaynaktan türetilir | `BankLoan→Loan`, `CashAdvance→CashAdvance`, `PersonalBorrowing→PersonalDebt`; modüller arası FK yok |
| Slice 6/7 için `CreateFromDebtOwner(...)` ve `LinkToDebt(...)` hazır | Sahibi modül bağlantıyı aynı işlemde kurar; normal POST bu yolu kullanamaz |
| Borç bağlantılı kayıtta `PUT`/iptal `409 StateConflict` | Bağlı kayıt sahibi borç kaydından yönetilir |
| İptal gerekçesi zorunlu; iptal idempotent | docs/05 §4.4; ikinci iptal `204` |
| `recordedAtUtc` istemciden alınmaz; PUT'ta `400` | Sunucu zamanı |
| `from`/`to` `occurredOn` üzerinde; varsayılan `Active`, `includeCancelled` ile iptaller | docs/06 §5.2 |

### 4.9 Slice 6 — nakit avans (2026-11-17)

| Karar | Gerekçe |
|---|---|
| ADR-0009: handler paylaşılan `DbTransaction` açar, writer açmaz/commit etmez | Tek atomik sınır; transaction sahipliği net |
| `System.Data.Common.DbTransaction` sözleşmesi; ambient `TransactionScope`/outbox yok | SQL Server + SQLite testleriyle uyumlu; ek altyapı yok |
| `IUnitOfWork.Enlist(IWriteTransactionScope? scope)` nullable; `Enlist(null)` referansı temizler | Aynı DbContext test içinde scope sonrası yeniden kullanılabilsin |
| `DebtInflowCreate/Cancel` `Kind` + `DebtLinkId` taşır | Writer yalnızca beklenen borç kaydına bağlı girişi yazar/iptal eder |
| Avans iptali kart ödemelerini geri almaz | Borç yeniden hesaplanır; ödeme ayrı finansal olaydır |
| Borç formülü `expenses − refunds + cashAdvances − payments` | Avans kart borcunda bir kez görünür; ödeme ikinci gider değildir |

### 4.10 Slice 7 — kredi sözleşmesi (2026-11-15)

| Karar | Gerekçe |
|---|---|
| Girdi `Principal`, `MonthlyPayment`, `InstallmentCount`, `FirstDueDate` | Faiz hesabı yok; kullanıcının gerçek aylık ödemesi esas |
| `TotalRepayment = MonthlyPayment × InstallmentCount` türetilir | Tek doğruluk; kullanıcı girdisi değil |
| Taksitler eşit; son taksite yuvarlama farkı eklenmez | `MonthlyPayment` zaten taksit tutarıdır |
| `TotalRepayment >= Principal` doğrulaması korunur | Anapara üstü geri ödeme tutarlılığı |
| `LoanDto` `monthlyPayment` kullanır; ayrı `installmentAmount` yok | Kavram tekliği |
| Her ödenmiş taksit ödemesi iptal edilebilir; sıra kuralı yalnızca ödeme anında | 2026-11-17 onayı; iptal yalnızca ilgili taksiti `Planned` yapar |

### 4.11 Slice 9 — MAUI istemci (2026-11-18)

| Karar | Gerekçe |
|---|---|
| Hedefler Android + Windows; iOS/macOS kapsam dışı | Kullanıcı kararı (2026-11-17) |
| `FinTrack.Mobile.Core` (net10.0) platform bağımsız; MAUI projesi yalnızca UI + platform servisleri | Testler MAUI workload/SDK olmadan koşabilir |
| Tek TFM `Directory.Build.props` MAUI csproj'da `<TargetFramework></TargetFramework>` ile sıfırlanır | Çoklu hedef `TargetFrameworks` ile çalışır |
| `AuthHandler` 401'de refresh eder; refresh auth handler'sız ayrı `HttpClient` | Döngiye girmez; header sızdırmaz |
| Eşzamanlı refresh `SemaphoreSlim` single-flight; başarısız refresh token'ları siler | Tek oturum olayı, eski token ile devam yok |
| Otomatik retry yalnızca GET/HEAD/OPTIONS | `Idempotency-Key` yok → POST/PUT çift kayıt riski |
| Token'lar `SecureStorage`'da; log/hata mesajı yok | Sızdırma yok; hatalar `ApiException` alanlarıyla eşleşir |
| `IQueryProperty` yok → `IQueryAttributable.ApplyQueryAttributes` | .NET 10 MAUI API'si |
| `MainPage` deprecated → `Application.CreateWindow` + `Windows[0].Page` | .NET 10 yaşam döngüsü |
| Release XAML `x:DataType` + `DataTemplate x:DataType`; Picker `ItemDisplayBinding` yerine DTO `ToString()` | Release build 0 uyarı, derlenmiş binding |
| v1 çevrimdışı yazma yok; okuma önbelleği de eklenmedi | Sunucu doğrulama otoritesi; kapsam dışı |

---

## 5. Uygulama notları ve tuzaklar

- **EF çeviri sırası:** `IExpenseQueries` filtreleri `Expense` üzerinde, projeksiyondan
  **önce** uygulanır. `ExpenseRow` üzerinde filtre → `InvalidOperationException`.
- **Yeni iade:** `IExpenseRepository.AddRefundAsync` (`db.Refunds.AddAsync`) şart.
  Navigasyondan keşfedilen anahtarlı varlık `Modified` sanılıp sahte
  `ConcurrencyConflict` üretir.
- **SQLite test modeli:** varsayılan şema yok; `rowversion` 8 baytlık manuel değer,
  test-only interceptor ile artırılır. Gerçek eşzamanlılık API/MSSQL testlerinde.
- **Global filtre:** `ICurrentUser.UserId ?? Guid.Empty`.
- **Smoke testleri DB'siz** (`WebApplicationFactory<Program>`); kimlik, harcama,
  kart ve para girişi entegrasyon testleri Testcontainers MSSQL +
  `Database:MigrateOnStartup=true`.
- **Docker zorunlu (yerel):** Entegrasyon testleri Docker Desktop motoru kapalıyken
  `DockerUnavailableException` ile toplu başarısız olur (50 test); hata koda değil
  ortama aittir. Testten önce `docker info` çalıştığını doğrula. GitHub Actions
  `ubuntu-24.04` imajında Docker hazır gelir.
- **Ekstre türetimi:** `ComputedDebtAtPeriodEnd` ve `CarriedOverAmount` saklanmaz;
  her okumada `CardDebtService` + `ICardPaymentQueries` ile hesaplanır. Devreden
  borç aralığı `(önceki.PeriodEnd, mevcut.PeriodEnd]`; önceki aktif ekstre yoksa `0`.
- **İptal kesiti:** `CancelledAtUtc` UTC'dir; karşılaştırma için kesit gününün
  sonu `UserTime.EndOfDayUtc` ile bir kez UTC'ye çevrilir. EF sorgusunda saat
  dilimi çevirisi yapılmaz.
- **Ekstre dönem çakışması:** Yalnızca `CancelledAtUtc == null` ekstreler engel;
  kontrol + ekleme `Serializable` transaction içinde.
- **Kart ödemesi gider değildir:** Expenses'a kayıt yazmaz; yalnızca bakiye
  formülündeki `payments` terimini besler. Fazla ödeme negatif bakiye üretir.
- **Para girişi türetimi:** `CashInflow.IsEarnedIncome` ve `DebtType` kolon
  değildir; kaynaktan hesaplanır. `bool?` olduğu için `Unknown` JSON'da `null`
  döner ve gelir toplamlarına katılmaz.
- **Borç kaynakları:** Normal `POST /cash-inflows` `BankLoan`, `CashAdvance`,
  `PersonalBorrowing` kaynaklarını reddeder. Sahibi modül Slice 6/7'de
  `CreateFromDebtOwner(...)` kullanır; `LinkToDebt(...)` yalnızca borç
  bağlantısı olmayan kaydı bağlar. DB'de modüller arası FK yoktur.
- **Borç bağlantılı kayıt:** `DebtLinkId` doluysa `PUT` ve iptal
  `409 StateConflict` döner; iptal/güncelleme sahibi borç kaydından yapılır.
- **İptal idempotent:** İkinci iptal gerekçe doğrulamasına takılmadan `204`
  döner; ilk iptalde gerekçe zorunludur.
- **Paylaşılan transaction (ADR-0009):** Writer kendi transaction'ını açmaz;
  handler açar, yazar modüller `Enlist(scope)` ile DbContext'ini bağlar ve
  `finally` içinde `Enlist(null)` + dispose uygulanır. Aynı DbContext scope sonrası
  yeniden kullanılacaksa `Enlist(null)` şarttır; aksi halde EF eski transaction
  referansıyla "transaction object is not associated with the same connection"
  hatası verir.
- **Bağlı giriş doğrulaması:** `IDebtCashInflowWriter` `Kind` ve `DebtLinkId`
  eşleşmezse yazmaz/iptal etmez; aksi halde yanlış borç kaydı para girişini
  kapatabilir.
- **Avans iptali:** Kart ödemeleri geri alınmaz; yalnızca avans ve bağlı giriş
  kapanır, bakiye yeniden hesaplanır.
- **Fixture yapılandırması:** `ApiFixture` bağlantı dizesi/JWT değerlerini
  `WithWebHostBuilder` + `ConfigureAppConfiguration` ile vermek **yetmez**;
  `Program.cs` bunları host kurulurken doğrudan okur ve
  `appsettings.Development.json` kazanır. Değerler
  `ConnectionStrings__FinTrackDb`, `Database__MigrateOnStartup`, `Jwt__*`
  ortam değişkenleriyle verilir; `DisposeAsync` eski değerleri geri koyar.
  Bu düzeltmeden önce testler Windows'ta sessizce LocalDB'ye yazıyordu
  (`FinTrackDb` içinde 297 test kullanıcısı), Linux CI'da LocalDB olmadığı için
  patlıyordu.
- **`_status`:** Tüm modüllerden kaldırıldı (Expenses, CreditCards, CashInflows,
  Debts ve slice 8 ile Reporting); smoke testleri yerine route gruplarının
  `RequireAuthorization()` davranışını kontrol eder.
- **Bilinmeyen JSON alanı:** Minimal API gövde bağlama hatası varsayılan olarak
  Development'ta istisna fırlatır ve `UseExceptionHandler` bunu `500` yapar.
  `RouteHandlerOptions.ThrowOnBadRequest = false` ile tüm ortamlarda `400` döner.
  Kart isteklerinde `[JsonUnmappedMemberHandling(Disallow)]` alan adını reddeder.
- **Kart borcu:** `ICardExpenseQuery` Expenses altyapısında; iptal edilmiş harcama
  ve iadeler toplama girmez. Kart modülü Expenses tablolarına doğrudan erişmez.
- **Arşivli kart:** `GET /cards/{id}` ve `/balance` çalışır; `POST /expenses`
  `cardId` doğrulamasında arşivli kart `400 ValidationFailed` döner.
- **Kart `GET` filtresi:** `status` verilmezse `Active`; bilinmeyen değer
  `400 ValidationFailed` (`status` alanı).
- **Türkçe dosyalar UTF-8;** konsol çıktısı mojibake gösterebilir; düzenlemede
  ASCII çapa veya tam metin kullan.
- **Migration üretimi:** `dotnet ef migrations add ... --project ... --startup-project
  src/Host/FinTrack.Api`; tasarım zamanı fabrikası mevcut. Birden fazla DbContext
  olduğundan `--context CreditCardsDbContext` veya `--context CashInflowsDbContext`
  gibi açık seçim gerekir.
- **MAUI çoklu hedef:** `Directory.Build.props` tek TFM verdiği için MAUI
  csproj'da `<TargetFramework></TargetFramework>` gerekir; `-p:TargetFrameworks=...`
  geçirmek core projenin `net10.0` varlıklarını bozar. `-f <tfm>` kullan.
- **Android SDK/JDK:** Windows'ta varsayılan `JAVA_HOME` hâlâ JRE 1.8'i
  (`C:\Program Files\Java\jre1.8.0_451`, `jar` yok) gösterir; komut satırı
  derlemesinde `-p:JavaSdkDirectory='C:\Program Files\Eclipse Adoptium\jdk-17.0.21.9-hotspot'`
  geçirilir. JDK 17 + Android SDK 36 kuruldu; `net10.0-android` yerel olarak
  0 uyarı/0 hata derlenir. `adb install` fast-deployment ile `No assemblies
  found ... .__override__/x86_64` verir; emülatöre kurulum için
  `dotnet build ... -f net10.0-android -t:Install` kullanılır.
- **Android çalışma zamanı:** MAUI uygulaması için `MainApplication`
  (`MauiApplication` subclass) dosyası zorunlu; yoksa manifest
  `android.app.Application` kalır ve `MauiProgram.CreateMauiApp()` çalışmaz.
  Tek sayfalık başlangıçta global `//login` rotası `Global routes currently
  cannot be the only page on the stack` hatası verir; `login` kullanılır. XAML
  `Keyboard="Decimal"` geçersizdir, `Keyboard="Numeric"` kullanılır. Root
  sayfalarda `GoBackAsync` `..` yerine `//main`'e döner. Smoke için geçici
  `http://10.0.2.2:5241/api/v1/` ve `usesCleartextTraffic` ayarları geri
  alındı; varsayılan `https://10.0.2.2:7241/api/v1/`.
- **.NET 10 MAUI API:** `IQueryProperty` yok (`IQueryAttributable` kullan);
  `Application.MainPage` deprecated (`CreateWindow` kullan); `DisplayAlert` yerine
  `DisplayAlertAsync`.
- **Release XAML uyarıları:** `x:DataType` yoksa 135 `XC0022` uyarısı çıkar;
  kök sayfa + `DataTemplate` için `x:DataType` şart. Picker
  `ItemDisplayBinding` derlenemediğinden DTO `ToString()` kullanılır.

---

## 6. Ertelenen ve kapsam dışı

| Konu | Durum |
|---|---|
| `Idempotency-Key` (finansal/mutasyon POST, auth hariç) | **V11-T01 + V11-T02 tamamlandı**: host altyapısı ve gerçek mobil harcama akışı aynı key/gövde ile güvenle retry eder (V11-T01: 435/435; V11-T02: 45/45 mobil + çözüm build 0/0; `docs/11` §9.5–9.6) |
| Kart harcamaları için taksitli ödeme planı | v1 kapsam dışı |
| Çoklu para birimi / kur dönüşümü | v1 kapsam dışı |
| Kategori `restore`, arşivli kartı geri açma | v1'de yok |
| Rate limiting / kota | v1.1 |
| Tam audit log | v1'de durum + iade/iptal kayıtları yeterli |
| Banka API, kart numarası/CVV/SKT | Hiçbir zaman yok |
| Mobil çevrimdışı yazma | v1 kapsam dışı (Slice 9) |
| Mobil okuma önbelleği | Opsiyoneldi; eklenmedi |
| Bildirimler, biyometrik giriş, çoklu cihaz oturum ekranı | v1 kapsam dışı (Slice 9) |
| Android emülatör uçtan uca smoke | ✅ Tamamlandı (2026-11-18); `fintrack_smoke` AVD, giriş → harcama → kart → rapor |

---

## 7. Dilim kapanış notları

### 7.1 Slice 4 — Ekstre + kart ödemesi ✅ (2026-11-16)

**Bağlayıcı kaynak:** `docs/07` §4 Slice 4, `docs/05` §5.3–5.6, `docs/06` §6.3–6.6.

**Sonuç:** Domain (`CardPayment`, `Statement`), Application (handler'lar,
`CardDebtService`, `StatementDerivation`), Infrastructure (`CardPayments`,
`Statements`, migration, sorgular) ve Endpoints tamamlandı. Birim testleri
`137/137` yeşil; ekstre/ödeme entegrasyon testleri Docker kapalı olduğu için
yerelde çalıştırılamadı (CI'da 46 entegrasyon testi çalışacak).

**Kararlar (2026-11-15, onaylandı):**

| # | Konu | Karar |
|---|---|---|
| 1 | `computedDebtAtPeriodEnd` anı | `PeriodEnd` gününün sonu; kullanıcı saat dilimi; iş kuralı `DateOnly`; `asOf` ilgili `PeriodEnd` tarihine sabitlenir |
| 2 | `carriedOverAmount` | Önceki **aktif** ekstre (en büyük `PeriodEnd`); `max(0, önceki.StatementAmount − ((önceki.PeriodEnd, mevcut.PeriodEnd] aralığındaki, mevcut kesitte aktif ödemeler))`. Bilgi amaçlı; borca eklenmez; ödeme-ekstre bağı v1'de yok |
| 3 | Dönem çakışması | Yalnızca `Active` ekstreler engeller; iptal edilmiş ekstre yeni oluşturmayı engellemez |
| 4 | Ödeme iptali | `CancellationReason` zorunlu; `CancelledAtUtc` sunucuda UTC; `PaidOn` finansal gerçekleşme tarihi, iptal zamanı ayrı sistem zamanı; iptal UTC zamanı kullanıcı yerel tarihine çevrilir |
| 5 | Tarih kesiti | Finansal olaylarda `OccurredOn` / `RefundedOn` / `PaidOn` / `TakenOn` esas; geriye dönük kayıt geçmiş kesiti günceller; dönem sonundan sonraki iade geçmiş ekstreye girmez; yerel iptal tarihi kesitten sonra ise kayıt kesitte aktif, eşit veya önce ise iptal sayılır |
| 6 | `GET /cards/{id}/balance` | Opsiyonel `asOf=YYYY-MM-DD`; yoksa kullanıcının bugünü; ekstreyle aynı tarih semantiği; yanıtta `asOfDate` |
| 7 | Türetilen ekstre alanları | `CarriedOverAmount`, `ComputedDebtAtPeriodEnd`, `ReconciliationDifference` saklanmaz; okuma anında hesaplanır |
| 8 | Gelecek `asOf` | Kullanıcının bugününden sonraki tarih `400 ValidationFailed`; bugüne kırpılmaz |

**Kapsam özeti:**
- `CardPayment`: oluşturma, listeleme, iptal; bakiye formülüne `− payments`.
- `Statement`: `PeriodStart`, `PeriodEnd`, `DueDate`, `StatementAmount` manuel;
  `CarriedOverAmount`, `ComputedDebtAtPeriodEnd`, `ReconciliationDifference`
  okuma anında türetilir, saklanmaz.
- Ekstre/ödeme uç noktaları; `_status` değişmez (kartta yok).
- `ICardExpenseQuery` sözleşmesine kesitli toplam (`asOfDate` + kullanıcı saat
  dilimi) eklenmesi; mevcut tüm zamanlar toplamı geriye uyumlu kalır.
- Tarih kesiti: kullanıcı saat diliminde `DateOnly`, gün sonu; iptal UTC zamanı
  kullanıcı yerel tarihine çevrilir; harcama/iade/ödeme için aynı kural.
- `GET /cards/{id}/balance`: gelecek `asOf` `400 ValidationFailed`.

**Kapsam dışı:** Nakit avans (slice 6), otomatik ekstre içe aktarma, faiz/ücret
hesabı, ödeme-ekstre eşleştirmesi ve ekstreye özel ödeme kaydı.

**Test senaryoları (21/21 uygulandı):**

> Senaryolar Domain/Application birim testleri ve API entegrasyon testleriyle
> karşılandı. Senaryo sayısı ve numaraları `docs/07` ve `docs/09` ile aynıdır.

*Ödeme*
1. Ödeme bakiyeyi düşürür; gider toplamını değiştirmez.
2. Borcu aşan ödeme negatif bakiye (`isInCredit = true`) üretir.
3. Gerekçesiz iptal `400`; gerekçeli iptal `204`, bakiye geri yükselir.
4. İptal edilmiş ödeme `payments` toplamına girmez.
5. Gelecek `paidOn` `400`; arşivli karta ödeme `400`.
6. `rowVersion` uyuşmazlığı `409`.

*Ekstre türetimi*
7. Ekstre bakiyeyi ve gideri değiştirmez.
8. Önceki aktif ekstre yoksa `carriedOverAmount = 0`.
9. Devreden borç sınırları: önceki `PeriodEnd` günü ödeme **hariç**, mevcut
   `PeriodEnd` günü ödeme **dahil**.
10. Mevcut kesitte aktif olmayan ödeme devredenden düşülmez: iptal UTC zamanı
    kullanıcı yerel tarihine çevrildiğinde kesit tarihine **eşit veya önce** ise
    ödeme kesitte iptal sayılır. Kesitten **sonra** iptal edilen ödeme kesitte
    aktif sayılır ve devredenden düşülür.
11. `computedDebtAtPeriodEnd` yalnızca `PeriodEnd` günü sonuna kadarki hareketleri
    içerir; sonraki gün hareketleri hariçtir.
12. `RefundedOn <= PeriodEnd` iade hesaplanan borcu düşürür;
    `RefundedOn > PeriodEnd` iade geçmiş ekstreyi değiştirmez.
13. Geçmiş tarihli iade/ödeme veya iptal sonradan girilirse ekstre yeniden
    okunduğunda `carriedOverAmount`, `computedDebtAtPeriodEnd` ve fark güncellenir.
    İptal zamanı UTC'den kullanıcı yerel tarihine çevrilir:
    - yerel iptal tarihi kesitten **önce veya eşit** ise kayıt kesitte iptal
      sayılır, toplama girmez;
    - yerel iptal tarihi kesitten **sonra** ise kayıt kesitte aktif sayılır,
      toplama girer;
    - aynı kural harcama, iade ve ödeme için geçerlidir.
14. `reconciliationDifference = statementAmount − computedDebtAtPeriodEnd`;
    otomatik gider/borç kaydı oluşmaz.
15. Aktif ekstreyle çakışan dönem `400 ValidationFailed`; iptal edilmiş
    ekstreyle çakışan dönem `201`.
16. `periodStart > periodEnd` ve `dueDate < periodEnd` → `400`.

*Tarih kesiti ve iptal*
17. Kesitten önce veya kesitle **aynı kullanıcı yerel gününde** iptal edilen
    harcama/iade/ödeme kesit hesabına girmez; kesitten sonra iptal edilen girer.
    `CancelledAtUtc` UTC zamanı kullanıcı saat dilimine çevrilerek karşılaştırılır.
18. Kullanıcı saat dilimi farklıyken gün sonu kesiti doğru ana çevrilir
    (örn. UTC+14 ve UTC-11).

*Bakiye `asOf`*
19. `asOf` yoksa kullanıcının bugünkü tarihi kullanılır.
20. Geçmiş `asOf` sonraki hareketleri hariç tutar; yanıtta `asOfDate` döner.
21. Geçersiz `asOf` formatı ve gelecek tarihli `asOf` (kullanıcının bugününden
    sonrası) `400 ValidationFailed` döner; gelecek tarih bugüne kırpılmaz.

### 7.2 Slice 5 — Para girişi ✅ (2026-11-16)

**Bağlayıcı kaynak:** `docs/07` §4 Slice 5, `docs/05` §4, `docs/06` §5.

**Sonuç:** Domain (`CashInflow`, `EarnedIncomeFor`, `CreateFromDebtOwner`,
`LinkToDebt`), Application (oluşturma/güncelleme/iptal + liste/detay),
Infrastructure (`inflows` şeması, migration, global filtre, sorgular) ve
Endpoints (`docs/06` §5) tamamlandı. 48 modül + 5 entegrasyon testi eklendi;
toplam **235/235** test yerelde yeşil. `CashInflows` `_status` uç noktası
kaldırıldı.

**Kapsam özeti:**
- `CashInflow` alanları: `amount`, `currency`, `occurredOn`, `source`,
  `description?`, `recordedAtUtc`, `status`, `debtLink`, `rowVersion`.
- `IsEarnedIncome` kaynaktan türetilir; `Unknown` → `null`.
- `BankLoan`, `CashAdvance`, `PersonalBorrowing` doğrudan oluşturulamaz.
- `PUT` ve iptal borç bağlantılı kayıtta `409`; iptal gerekçeli ve idempotent.
- Liste filtreleri: `from`, `to`, `source`, `status`, `includeCancelled`,
  `page`, `pageSize` (varsayılan `Active`).

**Kapsam dışı:** Borç kaydı oluşturma (slice 6/7), raporlama (slice 8).

**Doğrulama:**
- Build 0 hata/0 uyarı (Debug + Release).
- Birim testleri 185/185 (48 CashInflows dahil).
- Entegrasyon testleri 50/50 (Docker Desktop açık).
- Migration `20261116182933_InitialCashInflows`; LocalDB'de uygulandı,
  `has-pending-model-changes` → "No changes".

### 7.3 Slice 6 — Nakit avans ✅ (2026-11-17)

**Bağlayıcı kaynak:** `docs/07` §4 Slice 6, `docs/05` §5.5, `docs/06` §6.5,
[ADR-0009](adr/0009-moduller-arasi-atomik-yazma.md).

**Sonuç:** Domain (`CashAdvance`, `CashAdvanceStatus`, domain hataları),
Application (create/cancel handler'ları, `ICashAdvanceRepository`,
`ICashAdvanceQueries`, `CardDebtService` avans terimi), Infrastructure
(`cards.CashAdvances`, EF config, repository/sorgu, migration) ve Endpoints
(`GET/POST /cards/{id}/cash-advances`, iptal) tamamlandı. ADR-0009 ile avans ve
bağlı `CashInflow` aynı `DbTransaction` içinde oluşturulur/iptal edilir; hata
halinde iki kayıt da rollback olur. 21 yeni modül testi eklendi; birim/mimari
**206/206**, CI dahil toplam **261/261** yeşil.

**Kapsam özeti:**
- Bakiye: `expenses − refunds + cashAdvances − payments`; avans bir kez sayılır.
- Bağlı giriş: `source = CashAdvance`, `isEarnedIncome = false`,
  `DebtLinkId = cashAdvance.Id`; normal `POST /cash-inflows` bu kaynağı reddeder.
- İptal gerekçeli; avans ve bağlı giriş birlikte kapanır; kart ödemeleri geri
  alınmaz.
- Liste: `status`, `includeCancelled`, sayfalama; arşivli kartta yeni avans `400`.

**Doğrulama:**
- Debug + Release build 0 hata/0 uyarı; birim/mimari 206/206.
- Migration `20261117174437_AddCashAdvances`; `has-pending-model-changes` →
  "No changes".
- 5 yeni API entegrasyon testi CI run `44141605019` ile yeşil; toplam 261/261.

### 7.4 Slice 7 — Kredi + kişisel borç ✅ (2026-11-17)

**Bağlayıcı kaynak:** `docs/07` §4 Slice 7, `docs/05` §6, `docs/06` §7,
[ADR-0009](adr/0009-moduller-arasi-atomik-yazma.md).

**Sonuç:** Domain (`Loan`, `Installment`, `InstallmentPayment`, `PersonalDebt`,
`PersonalDebtRepayment`), Application (create/get/list/cancel, taksit ödeme ve
iptal, geri ödeme ekleme/iptal), Infrastructure (`debts` şeması, EF config,
repository/sorgu, migration) ve Endpoints (`GET/POST /loans`,
`GET/POST /personal-debts`, taksit ve geri ödeme alt kaynakları) tamamlandı.
ADR-0009 ile kredi/borç ve bağlı `CashInflow` aynı `DbTransaction` içinde
oluşturulur/iptal edilir. 31 modül + 8 API entegrasyon testi eklendi; birim/mimari
**237/237** yerelde yeşil, toplam **300/300** CI run `44150709461` ile doğrulandı.

**Kapsam özeti:**
- Kredi: `TotalRepayment = MonthlyPayment × InstallmentCount` türetilir;
  başlangıçta tüm vade için eşit taksit planı oluşturulur; 31'in ay sonuna
  sabitleme uygulanır.
- Kalan borç: kredide `TotalRepayment − Σ(ödenen taksitler)`, kişisel borçta
  `Principal − Σ(aktif geri ödemeler)`.
- Taksit ödeme sıralı ve tam tutarlidir; ödeme gider değildir. Sıra kuralı
  yalnızca ödeme anında uygulanır; ödenmiş herhangi bir taksitin ödemesi iptal
  edilebilir ve ilgili taksit tekrar `Planned` olur.
- İptal: kredide ödenmiş taksit, kişisel borçta aktif geri ödeme varsa `409
  StateConflict`; aksi halde aggregate ve bağlı para girişi birlikte kapanır.
- `rowVersion` kredi/borç için aggregate'e, taksit ödeme iptali ve geri ödeme
  iptali için alt kayda aittir; bayat token `409 ConcurrencyConflict`.
- `totalRepayment` / `remainingAmount` istemci gönderirse `400`;
  `installmentAmount` ve `linkedCashInflowId` DTO'da yer almaz.
- Debts `_status` uç noktası kaldırıldı; smoke testleri yerine iki route grubunun
  authorization kontrolü eklendi.

**Doğrulama:**
- Debug + Release build 0 hata/0 uyarı; birim/mimari 237/237 yerelde.
- Migration `20261117180809_InitialDebts`; `has-pending-model-changes` →
  "No changes have been made to the model since the last migration."
- 8 yeni API entegrasyon testi (oluşturma/liste/iptal, taksit ödeme/iptal,
  geri ödeme/iptal, kullanıcı izolasyonu, türetilmiş alan reddi, 31'e ay sonu,
  atomik giriş bağlantısı) CI run `44150709461` ile yeşil; toplam 300/300.

### 7.5 Slice 8 — Raporlama (tamamlandı)

**Bağlayıcı kaynak:** `docs/07` §4 Slice 8, `docs/05` §7 ve §9/11–16, `docs/06` §8.

- 3 GET uç noktası: `/reports/monthly?year=&month=`, `/reports/debt-summary`,
  `/reports/upcoming-payments?days=30` (varsayılan 30, aralık 1–90).
- Reporting 3 projeli (Application/Infrastructure/Endpoints); Domain ve
  DbContext/migration yok. Okuma sözleşmeleri `FinTrack.ModuleContracts`'te;
  adapter'lar veri sahibi modüllerin **Application** katmanında.
- Aylık özette iade `RefundedOn` döneminde orijinal kategoriye negatif gider
  olarak yansır; `isBankBalance: false` ve açıklama her zaman döner.
- Borç formülü `Σ(max(0, kartBorc)) + kalan kredi + kalan kişisel borç`;
  nakit avans bir kez, ekstre borca eklenmez, kart alacağı ayrı gösterilir.
- Yaklaşan ödemeler planlanan taksitler + aktif ekstre vadeleri; bilgi amaçlı,
  yeni kayıt oluşturmaz.
- `_status` kaldırıldı; route grubu `RequireAuthorization()`. Varsayılan saat
  dilimi `Europe/Istanbul`, kullanıcı tercihi `ICurrentUser.PreferredTimeZone`.
- 10 modül + 7 entegrasyon testi; toplam 320/320, CI run `44156632893` yeşil.

### 7.6 Slice 9 — MAUI istemci ✅ (2026-11-18)

**Bağlayıcı kaynak:** `docs/07` §4 Slice 9, `docs/06` §2–§8.

**Sonuç:** `maui-android` + `maui-windows` iş yükleri kuruldu. Üç yeni proje:
`FinTrack.Mobile.Core` (net10.0), `FinTrack.Mobile` (Android + Windows),
`FinTrack.Mobile.Tests` (15 xUnit). Shell + `CommunityToolkit.Mvvm`, typed
`HttpClient`, `ApiJson` DTO eşlemesi, `SecureStorageTokenStore`, `AuthHandler` +
`SessionRefresher` (401 refresh, single-flight, POST retry yok), 10 ViewModel
ve v1 ekranları (giriş/kayıt, özet, harcamalar + ekleme, kartlar + detay, borçlar,
raporlar, ayarlar) tamamlandı.

**Doğrulama:** Windows hedefi Debug + Release 0 uyarı/0 hata (`x:DataType`
uygulandı); 15 mobile testi dahil 263/263 birim/mimari Debug + Release; 72
entegrasyon testi değişmedi. Android hedefi yerel SDK/JDK eksikliği nedeniyle CI
Ubuntu job'ında derlendi (run `44189257561` yeşil); emülatör smoke'u yapılmadı.
Token'lar loglanmaz, hata mesajlarına eklenmez.

---

## 8. Açık sorular

### 8.1 Yanıtlananlar (2026-11-15)

| Soru | Karar |
|---|---|
| `docs/01` ayrı dosya olarak gerekli mi? | **Gerekli değil.** `docs/_source/` içindeki ham konuşma yeterli; ayrı dosya açılmayacak. |
| Sürüm kontrolü/CI başlatılsın mı? | **Evet.** Git deposu + commit'ler; GitHub Actions CI (`.github/workflows/ci.yml`) eklendi. Uzak depo [`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack); ilk çalıştırma yeşil (139/139). |
| Nakit avans ve raporlama geçişi | Nakit avans ve kredi **borç** olarak değerlendirilir. **Faiz/KKDF/BSMV hesabı yapılmaz**; aylık ödeme tutarı kullanıcıdan alınır. Slice 7 sözleşmesi `docs/06` §7.1'de netleştirildi. |
| Slice 4 — `computedDebtAtPeriodEnd` anı | `PeriodEnd` gününün sonu; kullanıcı saat dilimi; iş kuralı `DateOnly`; `asOf` ilgili `PeriodEnd` tarihine sabitlenir. |
| Slice 4 — `carriedOverAmount` | Önceki aktif ekstre; `(önceki.PeriodEnd, mevcut.PeriodEnd]` aralığındaki aktif ödemeler düşülür; `max(0, ...)`; bilgi amaçlı, borca eklenmez. |
| Slice 4 — dönem çakışması | Yalnızca `Active` ekstreler engeller; iptal edilmiş ekstre engel değildir. |
| Slice 4 — ödeme iptali | `CancellationReason` zorunlu; `CancelledAtUtc` sunucuda UTC; `PaidOn` finansal tarih, iptal zamanı ayrı sistem zamanı; iptal UTC zamanı kullanıcı yerel tarihine çevrilir. |
| Slice 4 — tarih kesiti | `OccurredOn`/`RefundedOn`/`PaidOn`/`TakenOn` esas; geriye dönük kayıt geçmiş kesiti günceller; kesitten sonraki iade geçmiş ekstreyi değiştirmez. İptal yerel tarihi kesitten önce veya eşitse kayıt iptal, sonraysa aktif sayılır; kural harcama/iade/ödeme için aynıdır. |
| Slice 4 — bakiye `asOf` | Opsiyonel `asOf=YYYY-MM-DD`; yoksa kullanıcının bugünü; ekstreyle aynı semantik; yanıtta `asOfDate`; gelecek tarih `400 ValidationFailed`. |
| Slice 4 — gelecek `asOf` | Kullanıcının bugününden sonraki `asOf` `400 ValidationFailed` döner; bugüne kırpılmaz. |
| Slice 5 — gelir niteliği | `IsEarnedIncome` kaynaktan türetilir; `Unknown` → `null`; istemci gönderirse `400 ValidationFailed`. |
| Slice 5 — borç kaynakları | `BankLoan`, `CashAdvance`, `PersonalBorrowing` normal uç noktadan oluşturulamaz; `400 DebtSourceRequiresOwnerEndpoint`. |
| Slice 5 — borç bağlantısı | DB'de nullable `DebtLinkId`; DTO `debtLink` türü kaynaktan türetilir; bağlantılı kayıtta `PUT`/iptal `409 StateConflict`. |
| Slice 5 — iptal | Gerekçe zorunlu; ikinci iptal idempotent `204`. |
| Slice 7 — kredi sözleşmesi | Girdi `Principal`, `MonthlyPayment`, `InstallmentCount`, `FirstDueDate`; `TotalRepayment` türetilir; taksitler eşit; son taksit yuvarlama düzeltmesi yok; `TotalRepayment >= Principal`. |

### 8.2 Açık kalanlar

- Slice 4 kapandı; 21 senaryo uygulandı ve dokümanlarla eşleşiyor.
- Slice 5 kapandı; 48 modül + 5 entegrasyon testi yeşil, dokümanlarla uyumlu.
- Slice 6 kapandı; ADR-0009 kabul edilip uygulandı, 21 modül + 5 entegrasyon
  testi yeşil; CI run `44141605019` ile toplam 261/261.
- Slice 7 kapandı; 31 modül + 8 entegrasyon testi eklendi; toplam 300/300,
  CI run `44150709461` yeşil.
- Slice 8 kapandı; 10 modül + 7 entegrasyon testi eklendi; toplam 320/320,
  CI run `44156632893` yeşil. Reporting DbContext/migration içermez.
- Slice 9 kapandı; 15 mobile testi eklendi; birim/mimari 263/263, entegrasyon
  72/72; toplam 335/335. MAUI iş yükleri kuruldu, Windows build 0/0; Android
  CI run `44189257561` ile yeşil.
- Android SDK/JDK 17 ve `fintrack_smoke` AVD kuruldu; manuel uçtan uca smoke
  giriş → harcama → kart → rapor akışıyla tamamlandı.

### 8.3 Oturum devri notu (2026-11-18)

**Depo durumu:** Slice 9 kodu `7b4e00a`, CI run ID'si `7455546`, CI bakımı
`a0286ad`, docs-only doğrulama `755eebb`; Android smoke düzeltmeleri ve
doküman güncellemesi `aa9efbe` commit'inde. Güncel HEAD ve çalışma ağacı için
`git log -1` / `git status` ile kontrol et. CI: run `44189257561` (kod,
335/335), `44190582258` (dokümanlar), `44365407221` (CI bakımı, 335/335),
`44366617692` (docs-only, 335/335) ve `44379256250` (Android düzeltmeleri,
335/335) `success`; `main` rozeti yeşil.

**Tamamlanan:** Slice 0–9 kod + test + doküman + CI + Android emülatör smoke.
Çözümde 33 proje; 263 birim/mimari + 72 entegrasyon = **335/335**. Backend ve
Windows MAUI Debug/Release 0 uyarı/0 hata; Android derlemesi yerel JDK 17 + SDK
36 ile 0 uyarı/0 hata, CI Ubuntu job'unda (Java 17 + `dotnet workload restore`)
yeşil. `fintrack_smoke` AVD'sinde giriş → harcama → kart → rapor akışı
doğrulandı. Release XAML `XC0022` uyarıları `x:DataType` ile temizlendi. Smoke
sırasında bulunan `MainApplication`, global `login` rotası, `Keyboard="Numeric"`
ve root `GoBackAsync` hataları düzeltildi; geçici HTTP/cleartext ayarları geri
alındı.

**Devam noktaları (öncelik sırası):**

1. **Android uçtan uca smoke ✅ (2026-11-18):** `fintrack_smoke` AVD, API
   `http://10.0.2.2:5241/api/v1/` üzerinden geçici olarak bağlandı. Kullanıcı
   `smoke_20261118@example.com` ile giriş yapıldı; API ile `Market` kategorisi
   oluşturuldu; UI ile 125,00 TRY harcama kaydedildi. Harcamalar `1 kayıt ·
   toplam 1 sayfa`, pano `Net harcama 125,00 TRY`, kartlar `0 aktif kart`,
   raporlar `Kategori kırılımı: Market` gösterdi. Geçici HTTP/cleartext ayarları
   kaldırıldı; final varsayılan `https://10.0.2.2:7241/api/v1/`.
2. **Windows manuel UI smoke (karar 2.1 — A, yapılacak):** Build 0/0 ama
   uygulama elle açılmadı; `giriş → harcama → kart → rapor` akışı Windows
   hedefinde de denenecek. V11-T01 doğrulama maddesi olarak kaydedildi; henüz
   çalıştırılmadı, operatör tarafından tamamlanacak. LocalDB smoke temizliği
   (karar 2.2-A) tamamlandı ve doğrulandı (0 satır).
3. **CI bakımı ✅ (2026-11-18):** Run `44190582258` annotasyonları giderildi —
   `checkout@v7`, `setup-dotnet@v6`, `cache@v6`, `setup-java@v6` ve
   `upload-artifact@v7` (hepsi Node 24); runner `ubuntu-latest` yerine
   `ubuntu-24.04` sabitlendi. `actionlint` v1.7.12 ile 0 hata. Push sonrası
   CI run `44365407221`, docs-only run `44366617692` ve Android düzeltmeleri
   run'ı `44379256250` yeşil (335/335).
4. **Ertelenen kapsam:** `Idempotency-Key` v1.1 görev kartı V11-T01
   (`docs/07` §5.1) **tamamlandı**; kararlar `docs/10` v1.2, altı koşullu onay
   ve son tasarım `docs/11` v1.3 + ADR-0010 uygulandı (§9.5).
   Okuma önbelleği opsiyoneldi, eklenmedi; bildirim / biyometrik giriş / çoklu
   cihaz oturum ekranı v1 dışı.

**Ortam notları:** `gh` CLI kurulu değil → GitHub API `curl.exe` ile kullanılıyor;
kimliksiz API 403 rate limit döndürebilir (HTML run sayfası alternatifi). PowerShell
`&&` desteklemez; `git rev-parse '@{u}'` tırnak ister. Yerel Docker daemon açık;
entegrasyon testleri 72/72 yerelde de koşuyor. `adb exec-out screencap -p`
binary dosyayı bozar; ekran görüntüsü için `adb shell screencap -p /sdcard/...`
+ `adb pull` kullanılır.

**Yeniden doğrulama komutları:**
`dotnet build src/Host/FinTrack.Api/FinTrack.Api.csproj -c Release --nologo` ·
`dotnet build src/Mobile/FinTrack.Mobile/FinTrack.Mobile.csproj -c Release -f net10.0-windows10.0.19041.0` ·
`dotnet build FinTrack.slnx -p:JavaSdkDirectory='C:\Program Files\Eclipse Adoptium\jdk-17.0.21.9-hotspot' --nologo` ·
`dotnet test FinTrack.slnx -p:JavaSdkDirectory='C:\Program Files\Eclipse Adoptium\jdk-17.0.21.9-hotspot' --nologo -v q` → 335/335 ·
`dotnet build src/Mobile/FinTrack.Mobile/FinTrack.Mobile.csproj -f net10.0-android -t:Install` (emülatör kurulumu için).

### 8.4 Kararlar v1.1 ve koşullu onaylar (2026-11-18)

Kaynak: `docs/10-karar-bekleyen-konulara-cevap.txt` ve `docs/11-kararlar.txt`
(yerel, commit edilmez).

| Konu | Karar | İşlendiği yer |
|---|---|---|
| Zorunluluk kapsamı | Finansal/mutasyon `POST`; auth hariç | `docs/05` §2.7, `docs/06` §1.4 |
| Anahtar kapsamı / gövde | userId + method + route template + route değerleri + key; **RFC 8785 uyumlu kütüphane** + SHA-256 (özel canonicalizer reddedildi) | `docs/11` §3.2 |
| Saklama / TTL | `shared.IdempotencyKeys`, host migration, 24 s tam gövde + 30 g gövdesiz tombstone, sınırlı temizlik | `docs/11` §5–6 |
| Çökme/atomiklik | 5 dk stale timeout **reddedildi**; aynı `DbConnection`/`DbTransaction`, tek commit, kalıcı `InProgress` yok — **koşullu onay alındı** | `docs/11` §4, ADR-0010 |
| Auth / hata replay / hata kodu | Auth kapsam dışı; sadece 2xx; `400 ValidationFailed`; **`409 IdempotencyExpired`** | `docs/06` §1.3–1.4 |
| Mobil | Restart'ta anahtar + şifreli gövde; SQLite + AES-256-GCM; `Uncertain`, durum sorgusu, otomatik yeni UUID yok — **koşullu onay alındı** | `docs/11` §7 |
| Test kapsamı | Tüm kapsam + **S1–S29** entegrasyon; RFC vektörleri, bağlantı kopması, çoklu instance matrisi | `docs/07` §5.1, `docs/11` §4.7 |
| Operasyonel | Windows smoke **bekliyor**; LocalDB temizliği **✅ tamamlandı**; git geçmişi dokunulmadı; son docs CI run'ı eklenmedi | `docs/07` §5.1, `docs/09`, `docs/11` §9.3 |
| v1.1 önceliği | **A — parola sıfırlama + e-posta doğrulama** | `docs/07` §5 |

**Altı koşullu onayın durumu** (`docs/11` §9.1): §4.2 tek transaction ✅ ·
§4.5 ADR-0010 + dört modül ✅ · §3.2 RFC 8785 kütüphane kapısı ✅ · §5.1 şema
ve veri koruması ✅ · §7.1 SQLite + AES-GCM ✅ · §7.3/R13 `Uncertain` + durum
sorgusu ✅. Dört kritik konunun çözümü `docs/11` §9.2'de, kalan riskler §9.4'te.

> Kullanıcı talimatı: **"Önce tasarımı göster, onaylayınca kod onayı ayrıca
> vereceğim."** Koşullu tasarım onayı ve açık kodlama onayı alındı; V11-T01
> uygulandı ve doğrulandı (`docs/11` §9.5). Kalan maddeler operasyoneldir.

### 8.5 Oturum devri notu (2026-11-19) — V11-T01 push sonrası

**Depo durumu:** V11-T01 kodu `5e614c7`
(`feat: implement V11-T01 host-owned idempotency and mobile pending operations`)
`origin/main`'e push edildi; öncüsü `5abb6af`, geçmiş yeniden yazılmadı. Bu
devir notu ve sürüm başlığı düzeltmeleri ayrı bir commit'dir; dönünce
`git status -sb` / `git log -3 --oneline` ile güncel HEAD'i doğrula.

**Tamamlanan:** Host idempotency middleware/store/`shared` migration/DI, tek
`DbConnection`/`DbTransaction` (ADR-0010 + dört modül), durum sorgusu ve
`409 IdempotencyExpired`, mobil AES-256-GCM pending store/kurtarma, S1–S29;
435/435 yerel test, host build 0/0. `docs/05`–`docs/11`, ADR-0010 ve README
tutarlı; ignore edilen yerel `docs/*cevap*.txt`, `docs/*kararlar*.txt` ve
`docs/answer*.txt` commit edilmedi. CI run `44413433056` (`5e614c7`)
**success**; Android MAUI head build JDK 17 ile yerelde 0/0 doğrulandı.

**Kalan (öncelik sırası):**

1. ✅ Push sonrası GitHub Actions `build-test` **yeşil** (run
   `44413433056`, `5e614c7`, 2026-11-18 22:38:02Z, 435/435 beklenen akış):
   `https://github.com/canbayerdogan/FinTrack/actions/runs/44413433056`.
   (Docs-only handoff commit `ce412cd` için ayrıca run tetiklendi mi kontrol
   edilebilir; kod doğrulaması `5e614c7` run'ı ile sağlandı.)
2. Windows manuel UI smoke: `giriş → harcama → kart → rapor` (karar 2.1-A);
   henüz çalıştırılmadı, operatör tarafından yapılacak.
3. ✅ Android MAUI head build JDK 17 ile 0 uyarı/0 hata:
   `dotnet build src/Mobile/FinTrack.Mobile/FinTrack.Mobile.csproj -f net10.0-android -p:JavaSdkDirectory='C:\Program Files\Eclipse Adoptium\jdk-17.0.21.9-hotspot'`
   (2026-11-19, yerel doğrulama). Windows hedefi build'i zaten yeşildi.
4. Yerel mobil test determinism düzeltmesi: `PendingOperationRecoveryTests` iki
   senaryosunda `PendingOperationStore`'a sabit `FakeTimeProvider` verildi;
   `FinTrack.Mobile.Tests` 31/31 yerelde yeşil (CI run `44413433056` zaten yeşildi).
5. Yeni kod işi yok; v1.1 sonrası öncelik parola sıfırlama + e-posta doğrulama.

**Geri dönüşte okunacaklar:** `docs/09` §2 → bu bölüm → `docs/11` §9.5 →
`docs/07` §5.1.

---

## 9. Değişiklik geçmişi

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-11-14 | İlk sürüm; slice 0–2 durumu, kararlar, tuzaklar, slice 3 hazırlığı |
| 1.1 | 2026-11-14 | Oturum protokolüne `docs/09` görev listesi eklendi |
| 1.2 | 2026-11-14 | Slice 3 tamamlandı: durum, test sayıları, kararlar, tuzaklar ve slice 4 hazırlığı güncellendi |
| 1.3 | 2026-11-14 | Açık sorular yanıtlandı: `docs/01` gerekli değil, git başlatıldı, borçlarda faiz yok/aylık ödeme kullanıcıdan |
| 1.4 | 2026-11-14 | Release build + 139/139 test doğrulandı; Docker kapalıyken entegrasyon testi tuzağı eklendi; depo/CI durumu güncellendi |
| 1.5 | 2026-11-14 | Entegrasyon fixture'ı env var ile bağlanıyor: testler artık gerçekten Testcontainers MSSQL kullanıyor (önceden sessizce LocalDB) |
| 1.6 | 2026-11-14 | GitHub uzak depo açıldı; ilk CI çalıştırması yeşil (139/139, Ubuntu) |
| 1.7 | 2026-11-15 | Slice 4 kararları (kesit, devreden borç, iptal, `asOf`) ve 21 test senaryosu eklendi; Slice 7 kredi sözleşmesi netleştirildi |
| 1.8 | 2026-11-15 | Gelecek `asOf` `400` kesinleşti; iptal UTC→yerel tarih kuralı ve aynı gün iptali senaryo 10/13/17'ye işlendi; 21 senaryo korundu |
| 1.9 | 2026-11-16 | Slice 4 tamamlandı: ekstre/ödeme domain-API-testleri, migration, 137 birim + 46 entegrasyon testi; Docker kapalı olduğundan entegrasyon CI'ya bırakıldı; sıradaki dilim Slice 5 |
| 2.0 | 2026-11-16 | Slice 5 tamamlandı: `inflows` şeması/migration, türetilmiş gelir niteliği, borç kaynağı reddi, iptal/bağlantı kuralları; 185 birim + 50 entegrasyon = 235 test yerelde yeşil; `_status` yalnızca Debts ve Reporting'de; sıradaki dilim Slice 6 (ADR-0009) |
| 2.1 | 2026-11-17 | ADR-0009 kabul edildi; Slice 6 tamamlandı: `cards.CashAdvances`, atomik avans + para girişi, iptal cascade, bakiye formülü, 21 modül + 5 entegrasyon testi; toplam 261/261, CI run `44141605019` yeşil |
| 2.2 | 2026-11-17 | Slice 7 tamamlandı: `debts` şeması, tam taksit planı, ödeme/iptal kuralları, kişisel borç ve geri ödemeler, ADR-0009 atomikliği; 31 modül + 8 entegrasyon testi, toplam 300/300, CI run `44150709461` yeşil |
| 2.3 | 2026-11-17 | Slice 8 tamamlandı: 3 salt okunur rapor uç noktası, veri erişimi yalnızca `ModuleContracts`, Reporting DbContext/migration yok, `_status` kaldırıldı; 10 modül + 7 entegrasyon testi, toplam 320/320, CI run `44156632893` yeşil |
| 2.4 | 2026-11-18 | Slice 9 tamamlandı: MAUI iş yükleri (Android + Windows), Core/Mobile/Tests projeleri, Shell + MVVM, SecureStorage + 401 refresh (POST retry yok), v1 ekranları; 15 mobile testiyle 263 birim/mimari + 72 entegrasyon = 335; Windows Debug/Release 0/0; Android CI run `44189257561` yeşil |
| 2.5 | 2026-11-18 | Oturum devri notu eklendi (§8.3): depo/CI durumu, kalan manuel smoke, CI bakım uyarıları ve yeniden doğrulama komutları |
| 2.6 | 2026-11-18 | CI bakımı tamamlandı: GitHub action'ları Node 24 major sürümlerine yükseltildi, runner `ubuntu-24.04` sabitlendi, `actionlint` doğrulaması eklendi |
| 2.7 | 2026-11-18 | CI bakım run'ı `44365407221` yeşil (335/335, `ubuntu-24.04`) dokümanlara işlendi; güncel HEAD `a0286ad` |
| 2.8 | 2026-11-18 | Android emülatör smoke'u ve bulunan platform hatalarının düzeltmeleri dokümanlara işlendi; docs-only CI run `44366617692` yeşil (335/335). Geçici HTTP/cleartext ayarları geri alındı, final API adresi `https://10.0.2.2:7241/api/v1/` |
| 2.9 | 2026-11-18 | Android düzeltmelerini içeren CI run `44379256250` yeşil (335/335, `ubuntu-24.04`); kod commit'i `aa9efbe` |
| 2.10 | 2026-11-18 | `docs/10` cevapları işlendi; `docs/11` teknik tasarımı, `docs/07` §5.1 V11-T01 görev kartı ve §8.4 karar özeti eklendi; "tüm POST" / "auth hariç" çelişkisi giderildi; kod yazılmadı, tasarım+kod onayı bekleniyor |
| 2.11 | 2026-11-18 | `docs/11-kararlar.txt` altı koşullu onayı işlendi: `docs/11` v1.1 + ADR-0010, RFC 8785 kütüphane kapısı, 24 s + 30 g tombstone, durum sorgusu/`409 IdempotencyExpired`, S1–S29, dört kritik konunun çözümü ve K1–K7 riskleri; LocalDB temizliği tamamlandı, Windows smoke bekliyor; yalnızca kodlama onayı bekliyor |
| 2.12 | 2026-11-19 | **V11-T01 uygulandı:** host idempotency (middleware/store/migration/DI, tek `DbTransaction`, ADR-0010 modül uyumları), durum sorgusu, mobil AES-256-GCM pending store ve kurtarma; 435/435 test. §4/§6/§8 durum notları güncellendi; Windows UI smoke ve MAUI head build operatörde |
| 2.13 | 2026-11-19 | V11-T01 kodu `5e614c7` ile commit edilip `origin/main`'e push edildi; "henüz commit edilmedi" notları ve sürüm başlıkları güncellendi, oturum devri kaydı §8.5'e eklendi. Push sonrası CI sonucu kontrol edilecek |
| 2.14 | 2026-11-19 | CI run `44413433056` (`5e614c7`) yeşil olarak doğrulandı; Android MAUI head build JDK 17 ile yerelde 0/0; mobil pending operation testlerinde deterministik saat enjeksiyonu düzeltildi (31/31). Kalan tek adım Windows manuel UI smoke (operatör) |
| 2.15 | 2026-11-20 | **V11-T02 uygulandı:** gerçek `AddExpenseViewModel.SaveAsync` akışı PendingOperation/Idempotency zincirine bağlandı; 14 uçtan uca mobil test eklendi (45/45), çözüm build'i 0/0, Docker hariç testler 354/354. §6, §8.7 ve `docs/09` v1.25 güncellendi; `docs/11` v1.4 uygulama notu eklendi. Docker daemon kapalı olduğu için API entegrasyon sonucu ve Windows UI smoke doğrulanmadı. |
| 2.16 | 2026-11-21 | **V12-T01 tamamlandı (yalnızca test):** Transaction Participant Architecture Audit F-01/F-09/F-10/F-12 için mimari koruma, gerçek SQLite connection/transaction kimliği, dört modülde `Enlist` guard'ı ve host `Serializable` doğrulaması eklendi. Docker hariç 379/379 test, çözüm build 0 uyarı/0 hata. Production kodu değişmedi; commit/push yok. Ayrıntı: `docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` §9. |
| 2.17 | 2026-11-21 | **Docs/test teslimi tamamlandı:** V12-T01 testleri ve devir notu commit edilip `origin/main`'e push edildi. §9 işaretçi uyuşmazlığı giderildi; audit tek dosyada birleştirildi (`...-2026-11-20.md` kopyası silindi), `docs/08`/`docs/09`/teslim raporu güncellendi. Test sayıları değişmedi (Docker hariç 379/379); production kodu değişmedi. |
| 2.18 | 2026-11-21 | **F-06/F-07/F-08 hata sınırı testleri eklendi:** `Enlist` istisnasının mevcut politikası, gerçek `DbTransaction.CommitAsync` istisnası ve gerçek ikinci yazıcının yazıp hata vermesi senaryoları test edildi. Yalnızca test + test paketi referansı; production kodu değişmedi. Docker hariç 383/383, build 0/0. Ayrıntı: audit raporu §9.6. |
| 2.19 | 2026-11-21 | **V12-T02 doğrulandı:** `committed` ölü state'i kaldırıldı, 2xx/expired commit'leri `CommitStatusChangeAsync` altında toplandı, S25 gerçeği düzeltildi, expired→Tombstone ve kapsam sınırı testleri eklendi. Audit F-03/F-04/F-05/F-11 + F-13 (yorum) kapatıldı; F-09/F-10/F-12/F-14 bu turda kapatılmadı. Docker hariç 390/390 + gerçek MSSQL 95/95, build 0/0, F-06 1/1, `diff --check` temiz. Beş kod/test dosyası commit/push bekliyor, yeni CI yok. §2, §8.11 ve bağlı belge sürümleri güncellendi. |

### 8.6 Sonraki oturum devri notu (2026-11-20)

**Bu oturumda yapılan (inceleme/raporlama):** docs/architecture-audit-mobile-idempotency.md oluşturuldu (Y-1/P-1 derin doğrulama). Kaynak kod bulgusu: V11-T01 altyapısı (PendingOperationStore, PendingOperationRecoveryService, AuthHandler koşullu retry) mevcut/DI kayıtlı/testli olsa da gerçek ViewModel/typed-client finansal mutasyon akışlarına entegre değil; AddExpenseViewModel.SaveAsync → CreateExpenseAsync doğrudan gönderim yapıyor, Idempotency-Key/pending kayıt/recovery tetiklemesi yok, App.OnStart recovery çalıştırmıyor.

**Sonraki oturum başlangıcı:**
1. Önce docs/architecture-audit.md ve docs/architecture-audit-mobile-idempotency.md dosyalarını oku.
2. Başlangıç promptu docs/12-PendingOperation Entegrasyonu.txt dosyasının içindedir; bu dosyayı okuyarak devam et.

**O anki durum (2026-11-20, §8.6 yazıldığı sırada):** Yerel HEAD `88441ac`,
`origin/main` `ce412cd` önündeydi ve V11-T02 henüz commit edilmemişti. Bu
not geçmiş durum kaydıdır; V11-T02 uygulama sonucu ve doğrulama için §8.7,
dosya sürümleri ve değişiklik geçmişi için §9 bakınız. Windows manuel UI smoke
(karar 2.1-A) hâlâ operatör bekliyor.

### 8.7 V11-T02 devir notu (2026-11-20) — gerçek mobil harcama akışı

**Kapsam:** `docs/12-PendingOperation Entegrasyonu.txt` görevi tamamlandı.
`AddExpenseViewModel.SaveAsync` artık `FinTrackApiClient.CreateExpenseAsync`'i
doğrudan çağırmıyor; `PendingOperationRecoveryService.CreateAsync` →
`SendAsync` / `RecoverOneAsync` zincirini kullanıyor. Tek gövde
`CreateExpenseRequest`'ten bir kez serileştirilip pending kaydına ve tüm
denemelere aynı baytlarla veriliyor; `AuthHandler` 401 sonrası aynı
`Idempotency-Key` + gövde ile retry ediyor.

**Mimari kararlar:**

- `FinTrackApiClient`'ın genel POST yardımcısı, pending durumu geçişini ve
  bayt-identik replay'i garanti edemediği için finansal POST
  `PendingOperationRecoveryService.SendPostAsync` üzerinden gönderiliyor.
  Typed client yalnızca form GET'leri için kullanılıyor; yeni paket, tablo,
  migration veya state machine eklenmedi.
- Kullanıcı izolasyonu yerel `UserIdKey = JWT sub` ile sağlanıyor
  (`JwtUserKey.TryGetSubject`, `SessionService.GetUserIdKeyAsync`). Recovery
  servisine isteğe bağlı `currentUserKeyProvider` verildi; oturum değişirse
  pending kayıt korunuyor ve başka token ile gönderilmiyor.
- `FindMatchingAsync` aynı kullanıcı/method/route ve gövde baytlarını arıyor;
  çözülemeyen gövde "yeni anahtar üretme" tarafında eşleşmiş sayılıyor.
  `Uncertain` kayıt otomatik yeni harcamaya dönüşmüyor.
- `PendingOperationStartup`, `App.OnStart` içinde navigasyondan sonra
  fire-and-forget çağrılıyor; `SemaphoreSlim(1,1)` kuyruğa girmeden tek uçuş
  garantisi veriyor, hatalar yutuluyor ve açılış/navigasyon bloklanmıyor.
- Görev kartındaki "30 saat" ifadesi V11-T01 kaynak tasarımındaki **30 günlük**
  tombstone TTL'siyle çelişiyor. Mevcut sözleşme korundu; 31. saatte durum
  sorgusu, 31. günde tombstone/`Uncertain` ayrı ayrı test edildi (`docs/11` §9.6).

**Doğrulama:**

| Kontrol | Sonuç |
|---|---|
| `dotnet test tests/FinTrack.Mobile.Tests --nologo` | ✅ **45/45** (31 mevcut + 14 yeni) |
| Birim/mimari/mobil toplam (Docker hariç) | ✅ **354/354** (45 mobil + 77 API birim + 10 mimari + 222 modül) |
| `dotnet build FinTrack.slnx -p:JavaSdkDirectory=... --nologo -v q` | ✅ 0 uyarı, 0 hata (Android + Windows hedefleri dahil) |
| `dotnet test FinTrack.slnx ... --nologo -v q` | ⚠️ Entegrasyon 86/95 `DockerUnavailableException` ile başarısız; 9 DB'siz smoke geçti. Docker endpoint `npipe://./pipe/docker_engine` kapalı. |
| Docker `docker info` | ⚠️ Zaman aşımı; daemon erişilemedi. |

**Açık kalan:** Windows manuel UI smoke; canlı API/uçtan uca Docker doğrulaması;
kart/borç/para girişi mutasyonlarının pending entegrasyonu (V11-T02 kapsam dışı);
profil `PUT`, login/register/logout (kapsam dışı). Yeni testlerin kapsamı ve Y-1
kapanış durumu `docs/architecture-audit-mobile-idempotency.md` §8'de listelendi.

### 8.8 V12-T01 notu (2026-11-21) — host transaction sahipliği testleri

Transaction Participant Architecture Audit sonucunda F-01/F-09/F-10/F-12 için
**yalnızca test** kapsamı güçlendirildi: dört yazma modulunda contract/host-atlama
mimari kontrolleri, gerçek SQLite'ta owner/writer connection + transaction kimliği,
dört UnitOfWork için tekrarlanan `Enlist`/`Enlist(null)` davranışı ve production
host fabrikasının `Serializable` isteği (fake `DbConnection` ile) doğrulandı.
Expenses/CreditCards yerel fabrikaları **korundu**; ihlal olarak işaretlenmedi.
Production kodu, NuGet paketi, migration ve transaction soyutlaması değişmedi.
Doğrulama: `FinTrack.slnx` build 0/0; Docker hariç 379/379 test geçti.
Docker gerektiren `FinTrack.Api.IntegrationTests` çalıştırılmadı. Ayrıntılar ve
açık kalan audit bulguları: `docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` §9.

### 8.9 Oturum devri notu (2026-11-21)

Bu oturumun kapanışında kapsam, doğrulama sonuçları, kısıtlar ve sıradaki adımlar
`docs/OTURUM-DEVIR-NOTU-2026-11-21.md` dosyasına kaydedildi. Sonraki oturumda §9
işaretçi uyuşmazlığı giderildi: V12-T01 eki
`docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` içine eklendi, öteki
tarihli kopya silindi; `docs/08`, `docs/09` ve teslim raporundaki bütün işaretçiler
bu tek dosyaya yönlendirildi. Audit gövdesi ve §9 bulguları değişmedi. V12-T01
testleri, bu düzeltmeler ve devir notu birlikte commit edilip `origin/main`'e
push edildi; Docker/Testcontainers doğrulaması hâlâ bekliyor.

### 8.10 F-06/F-07/F-08 hata sınırı testleri (2026-11-21, V12-T01 anlık görüntüsü)

Audit'in yüksek/orta seviyeli üç açık bulgusu için yalnızca test kapsamı eklendi:
`Enlist` istisnasında mevcut davranış (istisna yayılır, 500, idempotency kaydı
yazılmaz), production `WriteTransactionScope` üzerinde gerçek
`DbTransaction.CommitAsync` istisnasının yayılması ve middleware'ın bunu 409
`IdempotencyInProgress`'e çevirmesi, ayrıca gerçek ikinci yazıcının SQLite'a
yazıp sonra hata verdiğinde commit olmaması. F-08 testi fake writer yerine gerçek
`DebtCashInflowWriter` kullanır. Doğrulama: build 0/0, Docker hariç **383/383**
(Api.Unit 81/81, CreditCards 86/86). Docker/Testcontainers çalıştırılmadı;
F-06 için hata politikası kararı ve 3+ modül tek-transaction kanıtı hâlâ açık.
Ayrıntı: `docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` §9.6.

### 8.11 V12-T02 — commit yolu temizliği ve kapsam sınırı testleri (2026-11-21)

Gerçek Git diff'i (`HEAD` `1826dc7` ↔ çalışma kopyası) üzerinden doğrulanan
beş kod/test dosyası:

| Dosya | Özet |
|---|---|
| `src/Host/FinTrack.Api/Idempotency/IdempotencyMiddleware.cs` | `committed` ölü state'i kaldırıldı; iki commit yolu `CommitStatusChangeAsync` altında toplandı. Hook sırası ve exception handling korundu. |
| `src/Modules/CreditCards/.../CashAdvances/CashAdvanceHandlers.cs` | Yorum düzeltmesi: gerçek commit ADR-0010 host kapsamında middleware'da; host yoksa ADR-0009 akışı korunur. Çalışma zamanı kodu aynı. |
| `tests/FinTrack.Api.IntegrationTests/IdempotencyCoreScenariosTests.cs` | S25 adı/gerekçesi dört ayrı POST, dört ayrı `Idempotency-Key` ve dört ayrı transaction olacak şekilde düzeltildi. |
| `tests/FinTrack.Api.UnitTests/IdempotencyFailureBoundaryTests.cs` | Expired→Tombstone tek-commit regression testi eklendi. |
| `tests/FinTrack.Api.UnitTests/IdempotencyScopeBoundaryTests.cs` | **Yeni:** PUT/auth POST ve kimlik hatasında middleware scope'u açmadan `next`'e geçiş; kapsam-içi pozitif kontrol. |

**Kapatılanlar:** F-03 (ölü state), F-04 (tek commit yardımcı metodu),
F-05 (S25 gerçeği), F-11 (middleware bypassı — yalnızca scope açılmadığı ve
doğrudan `next`'e geçildiği kapsamda; endpoint içi domain write iddiası yok),
audit F-13 (güncel olmayan handler yorumu). Görev metnindeki "F-13 —
expired→Tombstone" ifadesi audit'de F-04'tür; ikisi ayrı kayıtlar olarak
kapatıldı.

**Doğrulama:** `dotnet build FinTrack.slnx --no-restore` 0 uyarı/0 hata;
Docker hariç testler **390/390**; gerçek Docker/Testcontainers + MSSQL API
entegrasyonları **95/95** (aynı testler iki kez sayılmadı); F-06 özel testi
**1/1**; `git diff --check` temiz. Windows manuel UI smoke yapılmadı. Yeni CI
çalıştırılmadı; son yeşil `build-test` `44931169879`, commit `1826dc7`,
V12-T02 öncesinedir. Beş dosya commit/push beklemektedir.

**Açık kalanlar:** F-09, F-10, F-14 açık; F-12 V12-T01'de kapatılmış kayıtlıdır
ve bu turda yeniden test edilmedi/kapatılmadı. F-06 politika kararı, 3+ modül
tek-transaction kanıtı ile Windows smoke bekliyor. Y-3 yapısal farkı
korundu; atomicity hatası kanıtlanmadığı için refactor önerilmedi.

**Önceden var olan riskler (V12-T02 regresyonu değil):**
`OperationCanceledException` → 409 (`IdempotencyMiddleware.cs:270-276`);
`RequestAborted` + cleanup gate (`IdempotencyCleanupService.cs:34`);
handler `Enlist(null)` sonrası `DisposeAsync()` atlanması
(`CashAdvanceHandlers.cs:106-107`, `:196-197`); rollback'in uncommitted
transaction dispose'a dayanması (`SqlWriteTransactions.cs:71-72`). Ayrıntı:
`docs/11` §9.4 K8–K11 ve audit raporu §10.5.
