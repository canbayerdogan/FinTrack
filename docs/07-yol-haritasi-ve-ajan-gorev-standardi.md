# FinTrack — Yol Haritası ve Ajan Görev Standardı

**Sürüm:** 1.11
**Tarih:** 2026-11-17
**Bağlı belgeler:** [Teknik Mimari v1.2](03-mimari.md) · [İş Kuralları v1.2](05-is-kurallari.md) · [API Sözleşmeleri v1.4](06-api-sozlesmeleri.md) · ADR-0001…0009
**Hedef okuyucu:** geliştirme ajanları ve geliştirici

> Bu belge iki şeyi tanımlar:
> 1. **Ne zaman, hangi sırayla** geliştirilecek (dilimler),
> 2. Bir ajanın bir görevi **hangi kurallarla** ele alacağı (görev standardı).
>
> Dilim sırası ve kabul kriterleri bağlayıcıdır. Değişiklik gerekiyorsa önce
> sorulur; ajan kendi başına sıra değiştiremez.

---

## 1. İlkeler

1. **Dikey dilim:** Her dilim uçtan uca çalışır — Domain → Application →
   Infrastructure → Endpoints → test → doküman. "Önce tüm veritabanı, sonra tüm
   API" yaklaşımı yoktur.
2. **Yeşil build:** Dilim, `dotnet build` ve `dotnet test` yeşil olmadan bitmiş
   sayılmaz.
3. **Test geri alınmaz:** Dilim eklediği testleri ve önceki zorunlu regresyon
   testlerini korur; silinemez, atlanamaz.
4. **Doküman koddan önce:** Davranış docs 02/03/05/06'da tanımlıdır. Kod bu
   belgelere uyar; belgeyle çelişen kod kabul edilmez.
5. **Açık karar varsa dur:** İş kuralı, alan adı veya kapsam boşluğu varsa ajan
   varsayım yapmaz; sorar.
6. **Modül sınırı pazarlık dışıdır:** Kısa yol için modül içi tip, DbContext veya
   tablo erişimi kullanılmaz.
7. **Kapsam genişletilmez:** Dilim kapsamı dışındaki "iyileştirme" aynı görevde
   yapılmaz; ayrı görev/ADR olarak önerilir.

---

## 2. Mevcut durum ve ortam

### 2.1 Tamamlanan

| Alan | Durum |
|---|---|
| Dokümanlar 01–07 | ✅ |
| ADR-0001…0009 | ✅ |
| Slice 0 — iskelet | ✅ 28 proje (slice 5 sonrası), 0 hata/0 uyarı |
| Slice 1 — kimlik doğrulama | ✅ Identity + JWT, `identity` migration, 22 birim + 17 entegrasyon testi |
| Slice 2 — harcama + kategori | ✅ `expenses` şeması, harcama/iade/kategori, 52 modül testi |
| Slice 3 — kart + hesaplanan bakiye | ✅ `cards` şeması, kart CRUD/arşiv/bakiye, 22 modül + 6 entegrasyon testi |
| Slice 4 — ekstre + kart ödemesi | ✅ `cards.CardPayments`/`cards.Statements`, ekstre türetimi, ödeme CRUD/iptal, `balance?asOf` |
| Slice 5 — para girişi | ✅ `inflows` şeması, `CashInflow` CRUD/iptal, kaynaktan türetilen `isEarnedIncome`, borç kaynaklarının reddi, 48 modül + 5 entegrasyon testi |
| Slice 6 — nakit avans | ✅ Kod/mimari tamam; `cards.CashAdvances`, ADR-0009 atomik yazma, avans + bağlı giriş, bakiye formülü, 21 modül + 5 entegrasyon testi (CI bekliyor) |
| Mimari testler | ✅ 3 test |
| Entegrasyon smoke testleri | ✅ 6 test (health + 2 modül `_status` + harcama/kategori/kart 401) |
| Entegrasyon kimlik testleri | ✅ 17 test (Testcontainers MSSQL) |
| Entegrasyon harcama testleri | ✅ 10 test (Testcontainers MSSQL, gerçek `ICardQuery`) |
| Entegrasyon kart testleri | ✅ 6 test (Testcontainers MSSQL) |
| Entegrasyon ekstre/ödeme testleri | ✅ 6 test (Testcontainers MSSQL) |
| Entegrasyon para girişi testleri | ✅ 5 test (Testcontainers MSSQL) |
| Entegrasyon nakit avans testleri | ✅ 5 test yazıldı/derlendi; yerel Docker motoru kapalı, CI doğrulaması bekliyor |
| Birim testleri (kimlik) | ✅ 22 test (doğrulama + JWT) |
| Modül testleri (Expenses) | ✅ 55 test (domain + handler, SQLite in-memory) |
| Modül testleri (CreditCards) | ✅ 78 test (domain + handler, SQLite in-memory; nakit avans dahil) |
| Modül testleri (CashInflows) | ✅ 48 test (domain + handler, SQLite in-memory) |

### 2.2 Ortam

| Konu | Değer |
|---|---|
| .NET SDK | `10.0.403` |
| Çözüm dosyası | `FinTrack.slnx` (`.sln` değil) |
| Test çalıştırma | `dotnet test FinTrack.slnx` |
| EF aracı | `dotnet-ef` `10.0.15` (yerel tool manifest: `dotnet-tools.json`) |
| Docker | CLI ✅ `29.7.2`; yerel daemon kapalı — Testcontainers MSSQL CI'da kullanılabilir |
| MAUI iş yükü | ❌ kurulu değil; slice 9'da `dotnet workload install maui` |
| Sürüm kontrolü | ✅ git (`main`) + GitHub uzak depo + GitHub Actions CI (yeşil) |
| NuGet sürümleri | Microsoft paketleri `10.0.15`; 11.x net11 hedefler, kullanılmaz |

### 2.3 Mevcut iskelet sınırları

- Modül `_status` uç noktaları **geçicidir**; ilgili dilim bitince kaldırılır.
  Slice 2 ile `Expenses`, slice 3 ile `CreditCards`, slice 5 ile `CashInflows`
  `_status` uç noktası kaldırıldı; yalnızca `Debts` ve `Reporting` kaldı
  (slice 7–8'de kaldırılacaktır).
- `ExpensesModule` harcama/kategori uç noktalarını, `expenses` DbContext'ini ve
  migration'ını içerir. `CreditCardsModule` kart/ekstre/ödeme/nakit avans uç
  `cards` DbContext'ini ve migration'ını içerir. `CashInflowsModule` para girişi
  uç noktalarını, `inflows` DbContext'ini ve migration'ını içerir. `DebtsModule`
  ve `ReportingModule` yalnızca DI ve route grubu kaydeder.
- Identity katmanı hazırdır: `FinTrack.Api/Identity`, `identity` şeması,
  `ApplicationUser`, `RefreshToken`, migration ve auth uç noktaları.
- Debts ve Reporting modüllerinde henüz DbContext, migration veya handler yoktur.
- `FinTrack.Modules.Expenses.Tests`, `FinTrack.Modules.CreditCards.Tests` ve
  `FinTrack.Modules.CashInflows.Tests` mevcuttur; Debts ve Reporting test
  projeleri ilgili dilimde oluşturulur.

---

## 3. Yol haritası özeti

| # | Dilim | Büyüklük | Bağımlılık | Durum |
|---|---|---|---|---|
| 0 | İskelet | S | — | ✅ |
| 1 | Kimlik doğrulama | M | 0 | ✅ |
| 2 | Harcama + kategori | L | 1 | ✅ |
| 3 | Kart + hesaplanan bakiye | L | 2 | ✅ |
| 4 | Ekstre + kart ödemesi | L | 3 | ✅ |
| 5 | Para girişi | M | 1 | ✅ |
| 6 | Nakit avans | M | 3, 4, 5 + ADR-0009 | ✅ kod/mimari (CI entegrasyonu bekliyor) |
| 7 | Kredi + kişisel borç | L | 5 + ADR-0009 | ⏳ sıradaki (CI koşuluyla) |
| 8 | Raporlama | L | 2–7 | ⏳ |
| 9 | MAUI istemci | L | 1–8 | ⏳ |

**Büyüklük:** S ≈ 1 oturum, M ≈ 2–3 oturum, L ≈ 4+ oturum. Takvim taahhüdü değildir.

> Slice 5 (para girişi) slice 3/4'ten bağımsızdır; 1'den sonra paralel
> ilerleyebilir. Ancak ajan aynı anda tek dilim üzerinde çalışır.

---

## 4. Dilim tanımları

Her dilimde ortak kabul: **0 hata / 0 uyarı build**, tüm testler yeşil, doküman
güncel, modül sınırları korunmuş.

### Slice 0 — İskelet ✅

- 24 projeli çözüm, SharedKernel, ModuleContracts, Api host.
- Health, OpenAPI/Scalar, Serilog, ProblemDetails.
- Modül başına route grubu + geçici `_status`.
- 3 mimari test, 6 entegrasyon smoke testi.

### Slice 1 — Kimlik doğrulama ✅

**Durum:** ✅ Bitti (2026-11-14). 22 birim + 17 entegrasyon kimlik testi yeşil.

**Amaç:** Kullanıcı kaydı, girişi, token yenileme ve kullanıcı izolasyonunun
temelini kurmak.

**Kapsam:**
- ASP.NET Core Identity (`IdentityUser<Guid>`) + JWT.
- `User` genişletmesi: `PreferredTimeZone` (IANA, varsayılan `Europe/Istanbul`),
  `CreatedAtUtc`.
- Identity DbContext ve `identity` şeması; migration.
- Access token 15 dk; refresh token 30 gün, **rotating**, DB'de hash'li.
- Endpoint'ler: `POST /auth/register`, `POST /auth/login`,
  `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`, `PUT /auth/me`.
- `ICurrentUser` implementasyonu (claim'den `UserId`).
- Parola politikası: 8–128 karakter, en az bir harf ve bir rakam.
- Global query filter altyapısı için `ICurrentUser` temeli.

**Kapsam dışı:** Parola sıfırlama, e-posta doğrulama, hesap silme, rate limiting,
çoklu cihaz oturum yönetimi ekranı.

**Dokunulan yerler:**
- `FinTrack.Api` içinde `Identity/` klasörü (ayrı iş modülü değildir; ayrı proje
  açmak ADR gerektirir).
- `FinTrack.SharedKernel` (`ICurrentUser` zaten var; implementasyon genişler).
- `FinTrack.Api.IntegrationTests` + yeni kimlik testleri.

**Testler:**
- Kayıt başarılı; e-posta normalize; zayıf parola reddi; mükerrer e-posta `409`.
- Giriş başarılı/başarısız; yanlış parola `401`.
- Refresh rotation: eski token geçersiz, yeni token çalışır.
- Logout sonrası refresh reddi.
- Token'sız korumalı uç nokta `401`.
- `PreferredTimeZone` geçersiz IANA reddi; `GET/PUT /auth/me`.
- Kullanıcı A, kullanıcı B'nin profilini göremez (404).
- Access token süresi/claim'leri doğru.

**Kabul kriterleri:**
- `docs/06` §2 sözleşmesine birebir uyum.
- Parola ve refresh token düz metin olarak loglanmaz/saklanmaz.
- Migration `identity` şemasında; testler Testcontainers MSSQL ile çalışır.
- `_status` uç noktaları henüz kaldırılmaz (diğer dilimler kullanıyor).

**Riskler:** Identity tablolarının şema yerleşimi; test başına DB izolasyonu;
refresh token rotasyonunda yarış koşulu.

---

### Slice 2 — Harcama + kategori ✅

**Durum:** ✅ Bitti (2026-11-14). 50 modül testi + 10 entegrasyon testi yeşil;
toplam 109 test.

**Amaç:** Harcama ve kategori yaşam döngüsünü uçtan uca çalıştırmak.

**Kapsam:**
- `Expense`, `Refund`, `Category` aggregate/entity'leri; `PaymentMethod`,
  `ExpenseStatus`.
- Kart alışverişi: `PaymentMethod = CreditCard` + `CardId`; **ayrı kart işlemi
  kaydı yok** (ADR-0008).
- İade: ayrı `Refund` kaydı; net tutar; durum türetimi.
- İptal: hard delete yok; gerekçe zorunlu.
- Kategori: kullanıcıya ait, case-insensitive benzersiz, arşivleme.
- Altyapı: `expenses` şeması, DbContext, migration, global query filter.
- Endpoint'ler: `docs/06` §3–4.
- `ICardQuery` sözleşmesi (kart doğrulaması için); slice 3'e kadar kart yoksa
  `CardId` doğrulaması "kart bulunamadı" döner.

**Kapsam dışı:** Kart CRUD, kart borcu, ekstre, raporlama, nakit avans.
`Idempotency-Key` desteği bu dilimde uygulanmadı; `docs/06` §1.4 sözleşmesi
gereği ayrı bir görev olarak ele alınacaktır (v1.1 hedefi).

**Dokunulan yerler:**
- `FinTrack.Modules.Expenses.{Domain,Application,Infrastructure,Endpoints}`
- `FinTrack.ModuleContracts` (`ICardQuery`)
- `tests/FinTrack.Modules.Expenses.Tests` (yeni)
- `FinTrack.Api.IntegrationTests`

**Testler:**
- Tutar `> 0`, tarih bugün/geçmiş, açıklama ≤ 500.
- `CreditCard` dışı ödeme yönteminde `CardId` reddi; `CreditCard`'ta zorunluluk.
- Kategori sahipliği, arşivli kategoriye yeni harcama reddi.
- İade toplamı harcamayı aşamaz; iade tarihi kuralları.
- İade sonrası `PartiallyRefunded`/`Refunded`; iade iptali net tutarı geri yükseltir.
- İadeli harcama iptal edilemez; iptal gerekçesi zorunlu.
- Güncelleme `recordedAtUtc` değiştirmez; iade toplamının altına düşüremez.
- Kullanıcı izolasyonu (404), sayfalama/filtreler.
- Mimari test: Domain'de EF Core/ASP.NET yok.

**Kabul kriterleri:**
- `docs/05` §3 ve `docs/06` §4 ile uyum.
- Harcama listesi `?cardId=` filtresini destekler.
- Kart alışverişi tek kayıt; ikinci gider kaydı oluşmaz (test).
- `_status` uç noktası kaldırılır.

**Riskler:** Kart modülü henüz yokken `CardId` doğrulaması; iade toplamında
eşzamanlılık; net tutarın raporlarda dönem mantığı (slice 8).

---

### Slice 3 — Kart + hesaplanan bakiye ✅

**Durum:** ✅ Bitti (2026-11-14). 22 modül + 6 entegrasyon testi yeşil;
toplam 139 test.

**Amaç:** Kart tanımı ve kart borcunun harcama/iadeden hesaplanması.

**Kapsam:**
- `CreditCard` aggregate; `Active`/`Archived`; arşivleme.
- Kart CRUD, `GET /cards/{id}/balance`.
- `ICardQuery` implementasyonu (Expenses doğrulaması için).
- `ICardExpenseQuery` sözleşmesi + Expenses tarafında implementasyon.
- Bakiye: `expenses − refunds` (ödeme/nakit avans henüz yok; sonraki
  dilimlerde formül genişler).
- Kart alışverişinin uçtan uca çalışması: `POST /expenses` + `cardId`.

**Kapsam dışı:** Ekstre, kart ödemesi, nakit avans, snapshot.

**Dokunulan yerler:**
- `FinTrack.Modules.CreditCards.*`
- `FinTrack.ModuleContracts` (`ICardQuery`, `ICardExpenseQuery`)
- `FinTrack.Modules.Expenses.Application` (sorgu implementasyonu)
- `tests/FinTrack.Modules.CreditCards.Tests` (yeni)

**Testler:**
- Kart CRUD, arşivleme; arşivli karta yeni harcama reddi.
- Kart alışverişi aylık giderde bir kez, bakiyede bir kez.
- İade bakiyeyi düşürür; gelir oluşturmaz.
- Bakiye negatif olabilir (kart alacağı).
- Modüller arası yalnızca sözleşme; doğrudan tablo erişimi yok (mimari test).
- Kullanıcı izolasyonu.

**Kabul kriterleri:**
- `docs/05` §5.1–5.3 ve `docs/06` §6 ile uyum.
- Kart için satın alma/işlem uç noktası **yok**; `GET /expenses?cardId=` çalışır.
- `_status` uç noktası kaldırılır.

**Riskler:** Modüller arası sorgu performansı; bakiye hesabında dönem filtresi
kullanılmaması; arşivli kartın geçmiş kayıtları.

---

### Slice 4 — Ekstre + kart ödemesi ✅

**Durum:** ✅ Bitti (2026-11-16). 57 modül + 6 entegrasyon testi; toplam 183
test. Migration: `20261116180727_AddCardPaymentsAndStatements`.

**Amaç:** Ekstre özeti ve kart ödemelerini ekleyerek kart borcu formülünü
tamamlamak.

**Kapsam:**
- `Statement`: `PeriodStart`, `PeriodEnd`, `DueDate`, `StatementAmount` manuel;
  `CarriedOverAmount`, `ComputedDebtAtPeriodEnd`, `ReconciliationDifference`
  okuma anında türetilir ve saklanmaz.
- Dönem çakışması: aynı kartta yalnızca `Active` ekstreler engeller; iptal
  edilmiş ekstre yeni oluşturmayı engellemez.
- Devreden borç: önceki aktif ekstre (en büyük `PeriodEnd`) üzerinden
  `max(0, önceki.StatementAmount − (önceki.PeriodEnd, mevcut.PeriodEnd]
  aralığındaki, mevcut kesitte aktif ödemeler)`; bilgi amaçlıdır, bakiyeye
  eklenmez. Ödeme-ekstre eşleştirmesi v1'de yoktur.
- `CardPayment`: oluşturma, listeleme, iptal; `PaidOn` finansal tarih,
  `CancelledAtUtc` sunucu UTC, iptal gerekçesi zorunlu.
- Bakiye: `expenses − refunds + cashAdvances(0) − payments`;
  `GET /cards/{id}/balance?asOf=YYYY-MM-DD` opsiyonel, yoksa kullanıcının bugünü,
  yanıtta `asOfDate`; gelecek `asOf` `400 ValidationFailed`.
- Tarih kesiti: kullanıcı saat diliminde `DateOnly`, gün sonu; finansal olaylarda
  `OccurredOn`/`RefundedOn`/`PaidOn`/`TakenOn`; iptal UTC zamanı kullanıcı yerel
  tarihine çevrilir ve kesitle aynı gün veya önceyse kayıt iptal sayılır.
- `ICardExpenseQuery` sözleşmesine kesitli toplam (`asOfDate` + kullanıcı saat
  dilimi) eklenmesi; mevcut tüm zamanlar toplamı geriye uyumlu kalır.
- Ekstre ve ödeme endpoint'leri (`docs/06` §6.3–6.6).

**Kapsam dışı:** Nakit avans (slice 6), otomatik ekstre içe aktarma, faiz/ücret
hesabı, ödeme-ekstre eşleştirmesi, ekstreye özel ödeme kaydı.

**Dokunulan yerler:**
- `FinTrack.Modules.CreditCards.*` (`CardPayment`, `Statement`, handler'lar,
  `CardDebtService`, `StatementDerivation`, EF config/migration, uç noktalar)
- `FinTrack.ModuleContracts` (`ICardExpenseQuery.GetCardTotalsAsOfAsync`)
- `FinTrack.Modules.Expenses.Infrastructure` (kesitli sorgu implementasyonu)
- `tests/FinTrack.Modules.CreditCards.Tests`, `tests/FinTrack.Modules.Expenses.Tests`,
  `tests/FinTrack.Api.IntegrationTests`

**Testler:**
- Ayrıntılı numaralı liste: `docs/08` §7 (21 senaryo); aşağıdakiler özet.
- Ekstre yeni gider/borç oluşturmaz; `statementAmount` bakiyeye eklenmez.
- Devreden borç türetilir; bakiyeye ikinci kez eklenmez.
- Ödeme gider değildir; bakiyeyi düşürür; fazla ödeme kart alacağı üretir.
- Ödeme iptali bakiyeyi geri yükseltir; gerekçesiz iptal reddedilir.
- Dönem çakışması yalnızca aktif ekstrelerde reddedilir; iptal edilmiş
  ekstreyle çakışan dönem oluşturulabilir.
- `dueDate >= periodEnd`, `periodStart <= periodEnd`.
- Mutabakat farkı yalnızca bilgi; otomatik kayıt yok.
- Kesit: dönem sonundan sonraki iade geçmiş ekstreyi değiştirmez; geçmiş
  tarihli iade/ödeme/iptal sonradan girilirse türetilen alanlar güncellenir.
  İptal UTC zamanı kullanıcı yerel tarihine çevrilir; yerel iptal tarihi kesitle
  aynı gün veya önceyse kayıt kesitte iptal, sonraysa aktif sayılır (harcama,
  iade ve ödeme için aynı kural).
- Saat dilimi sınırında gün sonu doğru ana çevrilir.
- Bakiye `asOf`: yoksa bugün; geçmiş kesit sonraki hareketleri dışlar; yanıtta
  `asOfDate` döner; gelecek `asOf` `400 ValidationFailed` ile reddedilir.
- Regresyon: `docs/05` §9/1, 8, 9.

**Kabul kriterleri:**
- `docs/05` §5.3–5.6, §9/1, 8–9 ve `docs/06` §6.3–6.6 ile uyum.
- `cardBorc` formülü belgelenen haliyle çalışır.
- Türetilen ekstre alanları okuma anında hesaplanır; geçmiş tarihli iade/iptal
  geçmiş ekstreyi güncelleyebilir.
- İptal, harcama/iade/ödeme için aynı kesit kuralıyla değerlendirilir; aynı
  yerel gün iptali kesitte iptal sayılır.
- Gelecek `asOf` `400 ValidationFailed` döner.
- İptal edilmiş ekstre dönem çakışması engeli oluşturmaz.
- Build 0 hata/0 uyarı, tüm testler yeşil, mimari testler yeşil.

**Riskler:** Ekstre tarihleri ile bakiye "as of" hesabı; saat dilimi sınırı;
iptal senaryolarında bakiye tutarlılığı; türetilen alanların okuma maliyeti.

---

### Slice 5 — Para girişi ✅

**Durum:** ✅ Bitti (2026-11-16). 48 modül + 5 entegrasyon testi; toplam 235
test. Migration: `20261116182933_InitialCashInflows`.

**Amaç:** Para girişlerini kaynağıyla kaydetmek ve gelir niteliğini türetmek.

**Kapsam:**
- `CashInflow` aggregate; `CashInflowSource` enum; `IsEarnedIncome` **türetilir**,
  kullanıcı set edemez.
- `inflows` şeması, DbContext, migration, global query filter.
- Endpoint'ler: `docs/06` §5.
- `Unknown` ayrı grup; `AssetSale` kazanç hesabı yok.
- Borç doğuran kaynaklar (`BankLoan`, `CashAdvance`, `PersonalBorrowing`) bu
  uç noktadan oluşturulamaz (`400 DebtSourceRequiresOwnerEndpoint`).
- Borç bağlantılı kayıtlar için sahibi modülün kullanacağı
  `CreateFromDebtOwner(...)` ve `LinkToDebt(...)` yolları hazır; DTO'da
  `debtLink` türü kaynaktan türetilir.
- İptal gerekçesi zorunlu ve idempotent; borç bağlantılı kayıtta doğrudan
  güncelleme/iptal `409 StateConflict`.

**Kapsam dışı:** Borç kaydı oluşturma (slice 6/7), raporlama (slice 8).

**Dokunulan yerler:**
- `FinTrack.Modules.CashInflows.{Domain,Application,Infrastructure,Endpoints}`
- `tests/FinTrack.Modules.CashInflows.Tests` (yeni)
- `FinTrack.Api.IntegrationTests` (para girişi + smoke `_status` temizliği)

**Testler:**
- Kaynak → `IsEarnedIncome` tablosu (docs/05 §4.2); `Unknown` → `null`.
- İstemci `isEarnedIncome` gönderirse `400 ValidationFailed`.
- Borç doğuran kaynakla doğrudan oluşturma `400`.
- `PUT` ve iptalde borç bağlantılı kayıt `409`; iptal gerekçesiz `400`,
  gerekçeli iptal `204`, ikinci iptal idempotent.
- Kullanıcı izolasyonu (404), sayfalama, `from`/`to`/`source`/`status`/
  `includeCancelled` filtreleri.
- OpenAPI şemasında para girişi uç noktaları var; `_status` yok.

**Kabul kriterleri:**
- `docs/05` §4 ve `docs/06` §5 ile uyum.
- `_status` uç noktası kaldırılır. ✅
- Build 0 hata/0 uyarı, tüm testler yeşil, mimari testler yeşil.

**Riskler:** `bool?` serileştirme; ileride borç modülleriyle bağlantı alanları.

---

### Slice 6 — Nakit avans ✅

**Durum:** ✅ Kod/mimari tamamlandı (2026-11-17). 21 yeni modül testi ve 5 yeni
API entegrasyon testi eklendi; birim/mimari **206/206** yeşil. Migration:
`20261117174437_AddCashAdvances`. Entegrasyon testleri derlendi; yerel Docker
motoru kapalı olduğu için son CI doğrulaması bekliyor.

**Amaç:** Nakit avansı kart borcuna bağlamak ve para girişiyle atomik oluşturmak.

**Ön koşul:** ✅ **ADR-0009 — modüller arası atomik yazma** kabul edildi
([ADR metni](adr/0009-moduller-arasi-atomik-yazma.md)). Sync writer + paylaşılan
`DbTransaction` seçildi; ambient `TransactionScope` ve outbox reddedildi.

**Kapsam:**
- [x] `CashAdvance` aggregate; kart borcu bileşeni (`cards.CashAdvances`).
- [x] Aynı kullanıcı işleminde `CashInflow` (`source = CashAdvance`,
      `isEarnedIncome = false`) oluşturma.
- [x] İptal: bağlı para girişi de iptal; kart ödemeleri geri alınmaz, borç yeniden
      hesaplanır.
- [x] Endpoint'ler: `GET/POST /cards/{id}/cash-advances`,
      `POST /cards/{id}/cash-advances/{cashAdvanceId}/cancel` (`docs/06` §6.5).
- [x] `CardDebtService`: `debt = expenses − refunds + cashAdvances − payments`.

**Kapsam dışı:** Nakit avans için ayrı geri ödeme kaydı (kart ödemesi kullanılır).

**Uygulama notları:**
- Transaction'ı sahibi modül handler'ı açar; writer transaction başlatmaz/commit
  etmez. Commit yalnızca scope üzerinden yapılır.
- Her yazan modül `IUnitOfWork.Enlist(scope)` ile DbContext'ini paylaşılan
  transaction'a bağlar; `Enlist(null)` testlerde referansı temizler.
- `IDebtCashInflowWriter` yalnızca beklenen `Kind`/`DebtLinkId` değerlerine bağlı
  girişi iptal eder; yanlış bağlantı `DebtInflowLinkMismatch` üretir.
- Avans iptali kart ödemelerini geri almaz; borç yeniden hesaplanır.

**Testler:**
- [x] Nakit avans toplam borçta **bir kez** (kart borcu içinde).
- [x] Bağlı para girişi kazanılmış gelir değil.
- [x] Yarım kayıt oluşmaz (gerçek SQLite `DbTransaction` rollback testleri).
- [x] İptal cascade; bakiye yeniden hesaplanır.
- [x] Kart ödemesi nakit avans borcunu azaltır; ikinci borç kalemi yok.
- [x] API sözleşmesi: oluşturma/liste/iptal, gerekçesiz iptal `400`,
      başka kullanıcının kartı `404`.

**Kabul kriterleri:**
- [x] `docs/05` §5.5, §9/2 ve `docs/06` §6.5 ile uyum.
- [x] ADR-0009 sonucu uygulanmış ve birim/mimari düzeyde test edilmiş.
- [ ] 5 yeni entegrasyon testi CI'da yeşil (yerel daemon kapalı).

**Riskler:** Atomiklik; iki modülün aynı DB'de farklı DbContext kullanması;
hata durumunda rollback. Bunlar ADR-0009 ve SQLite rollback testleriyle
doğrulandı; SQL Server tarafı CI'da doğrulanacak.

---

### Slice 7 — Kredi + kişisel borç

**Amaç:** Taksitli kredi ve kişisel borç yaşam döngüsünü kurmak.

**Ön koşul:** Slice 6'daki ADR-0009 mekanizması.

**Kapsam:**
- `Loan`, `Installment` (tüm vade planı), `PersonalDebt`, `PersonalDebtRepayment`.
- Kredi girdisi: `Principal`, `MonthlyPayment`, `InstallmentCount`, `FirstDueDate`;
  banka ve açıklama opsiyonel.
- `TotalRepayment = MonthlyPayment × InstallmentCount` türetilir; kullanıcı
  girdisi değildir. `TotalRepayment >= Principal` doğrulanır.
- Taksitler eşittir (`InstallmentAmount = MonthlyPayment`); son taksite otomatik
  yuvarlama farkı eklenmez. Ay sonu gün sabitleme uygulanır.
- Sıralı taksit ödeme; kısmi ödeme yok; ödeme iptali.
- Kredi/kişisel borç oluştururken bağlı `CashInflow` atomik oluşur.
- Kredi iptali (ödenmiş taksit varsa `409`), kişisel borç iptali (aktif geri
  ödeme varsa `409`).
- Endpoint'ler: `docs/06` §7.

**Kapsam dışı:** Faiz/KKDF/BSMV kırılımı, erken kapama, yeniden yapılandırma.

**Testler:**
- `monthlyPayment × installmentCount = totalRepayment`; taksitler eşit.
- `totalRepayment < principal` reddi; istemci `totalRepayment` gönderirse `400`.
- Ayın 31'i → şubat son günü.
- Planlanan taksit ödenmiş sayılmaz; kalan borç = planlanan toplam.
- Sırasız ödeme reddi; ödeme iptali taksiti `Planned`'a döndürür.
- Kredi/kişisel borç para girişi kazanılmış gelir değil.
- Atomik oluşturma/iptal; yarım kayıt yok.
- Kişisel borç kalan tutar; aşırı geri ödeme reddi.

**Kabul kriterleri:**
- `docs/05` §6, §9/3–4, 10 ve `docs/06` §7 ile uyum.
- `LoanDto` `monthlyPayment` ve türetilmiş `totalRepayment` içerir; ayrı
  `installmentAmount` alanı yoktur.
- `_status` uç noktası kaldırılır.

**Riskler:** Tarih aritmetiği; `monthlyPayment` ondalık hassasiyeti; atomiklik;
iptal kuralları.

---

### Slice 8 — Raporlama

**Amaç:** Aylık özet, borç özeti ve yaklaşan ödemeleri birleştirmek.

**Kapsam:**
- `GET /reports/monthly`, `/reports/debt-summary`, `/reports/upcoming-payments`.
- Kullanıcının saat diliminde takvim ayı; `OccurredOn`/`PaidOn`/`RefundedOn`.
- Kazanılmış gelir, diğer girişler, belirsiz girişler, brüt/net harcama,
  kategori kırılımı, `isBankBalance = false`.
- Toplam borç; nakit avans bir kez; negatif kart bakiyesi ayrı.
- Reporting salt okunur; veri `ModuleContracts`/okuma modeliyle gelir.

**Kapsam dışı:** Bütçe, tahmin, grafik altyapısı, dışa aktarma.

**Testler:**
- Doküman 02 §4 örneği birebir.
- Saat dilimi sınırı (ayın ilk/son günü).
- `Unknown` gelire katılmaz; diğer girişlere karışmaz.
- İade gerçekleştiği dönemde gideri azaltır.
- Toplam borç: kart + kredi + kişisel; nakit avans bir kez; ekstre eklenmez.
- Negatif kart bakiyesi toplam borçtan düşülmez.
- Yaklaşan ödemeler yeni kayıt oluşturmaz.
- Reporting başka modülün tablosuna doğrudan erişmez (mimari test).

**Kabul kriterleri:**
- `docs/05` §7, §9/11–12 ve `docs/06` §8 ile uyum.
- `_status` uç noktası kaldırılır.

**Riskler:** Modüller arası sorgu sayısı/performans; dönem sınırları; tutarların
gösterim yuvarlaması.

---

### Slice 9 — MAUI istemci

**Amaç:** v1 ekranlarını API'ye bağlamak.

**Ön koşul:** `dotnet workload install maui`; backend slice 1–8 tamam.

**Kapsam:**
- Shell + MVVM (`CommunityToolkit.Mvvm`), typed `HttpClient`.
- `SecureStorage`'da access/refresh token; 401'de otomatik refresh + tekrar.
- Ekranlar: giriş/kayıt, ana özet, harcama listesi/ekleme, kartlar, kart detayı
  (harcamalar, borç, ekstre), borçlar, kredi detayı, ayarlar.
- Ayarlar: saat dilimi (`GET/PUT /auth/me`), çıkış.
- v1: çevrimdışı yazma yok; yalnızca okuma önbelleği (opsiyonel).

**Kapsam dışı:** Çevrimdışı yazma, bildirimler, biyometrik giriş, çoklu cihaz
oturum ekranı.

**Testler:**
- ViewModel testleri (xUnit): doğrulama, hata eşlemesi, token yenileme akışı.
- Manuel: Android emülatöründe uçtan uca smoke.

**Kabul kriterleri:**
- `docs/06` sözleşmeleriyle uyum; token'lar loglanmaz.
- Cihazda giriş → harcama → kart → rapor akışı çalışır.

**Riskler:** MAUI iş yükü/emülatör kurulumu; platform farkları; SecureStorage.

---

## 5. v1.1 ve sonrası

| Konu | Not |
|---|---|
| `Idempotency-Key` zorunlu | Mobil ağ kopmalarında çift kayıt önlemi |
| Parola sıfırlama / e-posta doğrulama | Identity genişlemesi |
| Hesap silme (KVKK) | Soft delete + 30 gün |
| Çoklu para birimi | Model hazır; kur dönüşümü yok |
| Rate limiting / kota | API koruması |
| OpenTelemetry | İzleme |
| Ekstre belgesi yükleme / OCR | v1.1+ |
| Kart harcaması taksit planı | v1 kapsamı dışı |
| Kategori restore / kart arşivden çıkarma | v1'de yok |
| Tam audit log | v1'de durum + iade/iptal kayıtları yeterli |

---

## 6. Ajan görev standardı

### 6.1 Çalışma modeli

- Bir ajan **tek görev kartı** üzerinde çalışır; aynı anda birden fazla dilimi
  karıştırmaz.
- Görev kartı yoksa ajan önce kartı önerir; kapsam ve kabul kriterleri
  netleşmeden kod yazmaz.
- Görev, dilim kapsamını aşacaksa **durur ve sorar**.
- Ajan, kendi verdiği kararları ADR/görev kartına yazmadan uygulayamaz.

### 6.2 Görev öncesi kontrol listesi

1. `README.md`, `docs/02`, `docs/03`, `docs/05`, `docs/06` ve ilgili ADR'ler
   okundu mu?
2. Görev, yol haritasındaki hangi dilime ait? Bağımlılıkları tamam mı?
3. Mevcut build/test yeşil mi? (`dotnet build FinTrack.slnx`,
   `dotnet test FinTrack.slnx`)
4. Dokunulacak projeler ve modül sınırları net mi?
5. İş kuralı boşluğu / açık karar var mı? Varsa **kod yazmadan** soruldu mu?
6. Yeni paket gerekiyor mu? Gerekçe yazıldı mı?
7. Test stratejisi (unit / integration / architecture) belirlendi mi?
8. Migration gerekiyor mu? Hangi şema?

### 6.3 Görev kartı şablonu

Her görev aşağıdaki şablonla açılır:

```markdown
### Görev: S<n>-T<nn> — <kısa başlık>

**Dilim:** S<n>
**Büyüklük:** S / M / L
**Durum:** Planlandı / Devam / Bitti / Bloke

**Amaç:**
Tek paragraf; hangi kullanıcı/iş değeri.

**Kapsam:**
- ...

**Kapsam dışı:**
- ...

**Dokunulacak projeler:**
- ...

**API / sözleşme:**
- docs/06 §...; endpoint'ler.

**İş kuralları:**
- docs/05 §...; maddeler.

**Kabul kriterleri:**
- [ ] ...

**Testler:**
- Unit: ...
- Integration: ...
- Architecture: ...

**Doküman güncellemeleri:**
- ...

**Riskler / açık sorular:**
- ...
```

### 6.4 Uygulama sırası

Her dilimde önerilen sıra:

1. **Domain** — entity/value object/invariant/domain event; testler.
2. **Application** — command/query + handler + validator + portlar; testler.
3. **Infrastructure** — EF Core config, DbContext, migration, dış adaptörler.
4. **Endpoints** — minimal API, istek/yanıt eşleme, yetkilendirme.
5. **Integration testleri** — uçtan uca.
6. **Mimari testler** — sınır ihlali yok.
7. **Doküman/status** — README, docs, ADR, `_status` kaldırma.
8. **Rapor** — aşağıdaki formatta.

> Domain testi yazmadan Application'a, integration testi yazmadan "bitti"
> demeye geçilmez.

### 6.5 Kodlama kuralları

- C# 14 / .NET 10; `Nullable` ve `ImplicitUsings` açık.
- `sealed` sınıflar tercih edilir; DTO/command/query `record`.
- İş kuralı **Domain**'de; handler yalnızca akışı yönetir.
- `Result`/`DomainException` ile hata; HTTP'ye endpoint katmanında eşlenir.
- `DateTimeOffset`/`DateTime` yerine iş tarihleri `DateOnly`; damgalar UTC
  (`IClock.UtcNow`).
- Para `Money` value object; `float`/`double` yasak.
- EF Core yalnızca Infrastructure'da; Domain'de EF/ASP.NET referansı yok.
- Modüller arası yalnızca `FinTrack.ModuleContracts`; DbContext/tablo erişimi yok.
- Her aggregate `UserId` taşır; global query filter zorunlu.
- `async` + `CancellationToken` tutarlı kullanılır.
- MediatR, AutoMapper, MassTransit **kullanılmaz** (ADR-0003).
- Yeni paket: gerekçe + sürüm sabitleme; Microsoft paketleri `10.0.15`.
- Sırlar repoda tutulmaz; `appsettings.Development.json`'a gerçek anahtar yazılmaz.

### 6.6 Test standardı

| Katman | Zorunlu |
|---|---|
| Domain | Her invariant ve hesaplama için unit test |
| Application | Handler akışı, doğrulama, hata yolları |
| API | En az bir mutlu yol + bir hata yolu; auth/izolasyon |
| Mimari | Katman ve modül sınırı testleri yeşil |
| Regresyon | docs/05 §9'daki 12 madde kümülatif korunur |

- Testler kullanıcı davranışını doğrular; implementasyon detayına bağlı kırılgan
  test yazılmaz.
- Entegrasyon testleri Testcontainers MSSQL kullanır; Docker yoksa test
  atlanmaz, ortam düzeltilir.
- Test sırası/bağımlılığı yoktur; her test kendi verisini kurar.
- Hata testi yoksa dilim tamamlanmış sayılmaz.

### 6.7 Doküman standardı

- Davranış değiştiyse `docs/05`, sözleşme değiştiyse `docs/06`, yapı değiştiyse
  `docs/03`, karar değiştiyse ADR güncellenir.
- Kod ile doküman çelişirse **doküman esastır**; kod düzeltilir. Dokümanın
  yanlış olduğu düşünülüyorsa önce sorulur.
- Yeni iş kuralı = yeni ADR veya doküman değişikliği; sessiz varsayım yok.
- README "Mevcut durum" ve doküman tablosu görev sonunda güncellenir.
- `docs/08` durum ve devam notu ile `docs/09` görev listesi her dilim sonunda
  güncellenir; bağlayıcı kaynak değildir, çelişkide bu belge ve `docs/05`–`06` esastır.
- `_status` gibi geçici iskeleler dilim bitince kaldırılır ve testten çıkarılır.

### 6.8 Definition of Done (DoD)

Bir görev ancak aşağıdakilerin tamamıyla "Bitti" sayılır:

- [ ] `dotnet build FinTrack.slnx` → **0 hata, 0 uyarı**
- [ ] `dotnet test FinTrack.slnx` → tüm testler yeşil
- [ ] Yeni davranış için unit + integration testleri var
- [ ] docs/05 §9 regresyon testleri korunuyor
- [ ] Mimari testler yeşil; modül sınırı ihlali yok
- [ ] API, `docs/06` sözleşmesine uyuyor; OpenAPI üretilebiliyor
- [ ] Migration eklendi ve test DB'sinde uygulanıyor
- [ ] Hard delete yok; iptal/iade kuralları uygulanıyor
- [ ] Gerçek kart numarası/CVV/SKT veya banka giriş bilgisi yok
- [ ] Dokümanlar ve README güncel
- [ ] Geçici `_status` uç noktası (varsa) kaldırıldı
- [ ] Görev raporu yazıldı

### 6.9 Görev raporu formatı

```markdown
## Görev raporu — S<n>-T<nn>

**Özet:** ne yapıldı, neden.

**Değişen dosyalar:**
- ...

**Testler:**
- `dotnet build` → 0 hata/0 uyarı
- `dotnet test` → X/Y yeşil
- Eklenen testler: ...

**Kararlar / ADR:**
- ...

**Açık sorular:**
- ...

**Sıradaki:**
- ...
```

### 6.10 Yasaklar

- ❌ Modül A'nın DbContext'ine/tablosuna modül B'den erişmek
- ❌ Modüller arası FK veya join
- ❌ Finansal kayıtta hard delete
- ❌ Kart numarası / CVV / son kullanma tarihi / banka şifresi istemek veya saklamak
- ❌ Banka API'si veya ekstre OCR'ı eklemek (v1 kapsamı dışı)
- ❌ Harcama/ödeme/para girişi/borcu tek "işlem" tablosuna indirgemek
- ❌ `IsEarnedIncome` alanını kullanıcı girdisine açmak
- ❌ Kart alışverişini ikinci bir kayıt olarak tutmak
- ❌ MediatR/AutoMapper/MassTransit eklemek
- ❌ Testi silmek, atlamak veya `Skip` ile gizlemek
- ❌ Build uyarısını bastırmak (`NoWarn`, `#pragma`) gerekçesiz
- ❌ Görev kapsamı dışında "fırsat" değişikliği yapmak
- ❌ Açık kararı varsayımla kapatmak

### 6.11 Durma ve sorma tetikleyicileri

Aşağıdakilerden biri varsa ajan **kod yazmayı durdurur** ve sorar:

1. Dokümanlarda tanımlı olmayan iş kuralı gerekiyorsa,
2. İki doküman çelişiyorsa,
3. Yeni uç nokta/alan kapsamı genişletiyorsa,
4. Yeni dış bağımlılık veya altyapı gerekiyorsa,
5. Testler mevcut davranışla çelişiyorsa,
6. Migration geri dönüşü riskliyse,
7. Modül sınırını aşmadan çözüm yoksa.

Soru, önerilen seçenekler ve gerekçeleriyle birlikte sorulur; ajan tek başına
"en mantıklı" olanı seçip devam etmez.

---

## 7. Dilim geçiş kontrolü

Bir sonraki dilime geçmeden:

- [ ] Mevcut dilimin DoD listesi tamam
- [ ] `_status` kaldırıldı (ilgili modül)
- [ ] Regresyon testleri yeşil
- [ ] CI yeşil (`.github/workflows/ci.yml`; `main` push)
- [ ] README/doküman durumu güncel
- [ ] Bir sonraki dilimin bağımlılıkları (ADR dahil) hazır
- [ ] Açık soru kalmadı

Slice 1 için özel not: Kimlik altyapısı kurulmadan slice 2'ye geçilmez; çünkü
tüm modül sorguları `UserId` izolasyonuna dayanır.

---

## 8. Riskler ve bağımlılıklar

| Risk | Etki | Önlem |
|---|---|---|
| Modüller arası atomik yazma | Nakit avans/kredi ile para girişi yarım kalabilir | ✅ ADR-0009: sync writer + paylaşılan `DbTransaction`; SQLite rollback testleri, SQL Server CI'da |
| Test DB altyapısı | Integration testleri yavaş/kırılgan | Testcontainers MSSQL; test başına temiz şema |
| Hesaplanan kart borcu | Sorgu maliyeti | Önce doğruluk; gerekirse snapshot (kaynak doğruluk korunur) |
| Tarih/yuvarlama | Taksit ve rapor farkları | Domain testleri; gösterimde `AwayFromZero`; eşit taksit, son taksit düzeltmesi yok |
| Saat dilimi | Ay sınırı kayması | `DateOnly` + kullanıcı IANA; sınır testleri |
| MAUI iş yükü | Slice 9 gecikmesi | Backend'i önce bitir; MAUI en sona bırakılabilir |
| Git yokluğu | Değişiklik geri alınamaz | ✅ Çözüldü: git + GitHub uzak depo + zorunlu CI (`main` push/PR) |
| Kapsam kayması | v1 gecikir | Görev kartı + DoD + durma tetikleyicileri |

---

## 9. Değişiklik geçmişi

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-11-14 | İlk sürüm; dilimler, DoD ve ajan görev standardı |
| 1.1 | 2026-11-14 | Slice 1 (kimlik doğrulama) tamamlandı; 22 birim + 17 entegrasyon testi eklendi, toplam 48 test yeşil |
| 1.2 | 2026-11-14 | Slice 2 (harcama + kategori) tamamlandı; 50 modül + 10 entegrasyon testi eklendi, toplam 109 test yeşil; `Idempotency-Key` ayrı göreve ertelendi |
| 1.3 | 2026-11-14 | `docs/08` durum ve devam notu eklendi; §6.7 güncelleme kuralı genişletildi |
| 1.4 | 2026-11-14 | `docs/09` görev listesi eklendi; §6.7'ye dahil edildi |
| 1.5 | 2026-11-14 | Slice 3 (kart + hesaplanan bakiye) tamamlandı; 22 modül + 6 entegrasyon testi eklendi, toplam 139 test yeşil |
| 1.6 | 2026-11-14 | Git + GitHub uzak depo + GitHub Actions CI; ilk CI çalıştırması yeşil (139/139). Dilim kapanışında CI yeşili de aranır |
| 1.7 | 2026-11-15 | Slice 4 kapsam/kabul kriterleri tarih kesiti, iptal, devreden borç ve bakiye `asOf` kararlarına göre güncellendi; Slice 7 kredi sözleşmesi `monthlyPayment` girdisine ve eşit taksite geçti |
| 1.8 | 2026-11-15 | Gelecek `asOf` reddi ve aynı yerel gün iptal kuralı kabul kriterlerine eklendi; iptal kuralı harcama/iade/ödeme için birleştirildi; 21 senaryo referansı korundu |
| 1.9 | 2026-11-16 | Slice 4 (ekstre + kart ödemesi) tamamlandı; toplam 183 test, yeni migration ve kabul kriterleri işaretlendi; sıradaki dilim Slice 5 |
| 1.10 | 2026-11-16 | Slice 5 (para girişi) tamamlandı; `inflows` şeması/migration, türetilmiş gelir niteliği, borç kaynağı reddi ve iptal kuralları; 48 modül + 5 entegrasyon testi, toplam 235 test; `_status` yalnızca Debts ve Reporting'de kaldı; sıradaki dilim Slice 6 (ADR-0009 ön koşulu) |
| 1.11 | 2026-11-17 | ADR-0009 kabul edildi; Slice 6 (nakit avans) kod/mimari tamamlandı: `cards.CashAdvances` migration'ı, atomik avans + para girişi, iptal cascade, bakiye formülü, 21 modül + 5 entegrasyon testi; birim 206/206; entegrasyon CI doğrulaması bekliyor |
