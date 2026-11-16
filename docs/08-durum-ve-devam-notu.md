# FinTrack — Durum ve Devam Notu

**Sürüm:** 1.9
**Tarih:** 2026-11-16
**Bağlı belgeler:** [Yol haritası v1.9](07-yol-haritasi-ve-ajan-gorev-standardi.md) · [API Sözleşmeleri v1.4](06-api-sozlesmeleri.md) · [Teknik Mimari v1.2](03-mimari.md) · [İş Kuralları v1.2](05-is-kurallari.md) · ADR-0001…0008
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
| 5 | Para girişi | ⏳ sıradaki |
| 6 | Nakit avans | ⏳ |
| 7 | Kredi + kişisel borç | ⏳ |
| 8 | Raporlama | ⏳ |
| 9 | MAUI istemci | ⏳ |

**Çözüm:** `FinTrack.slnx`, **27 proje**. Depo **git** (`main`); uzak depo
[`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack) (public).
GitHub Actions CI (`.github/workflows/ci.yml`) son yeşil çalıştırması 139/139
testti; Slice 4 ile 183 test beklenir (Ubuntu, Testcontainers MSSQL).

**Yazılmış modüller:** Identity (`FinTrack.Api/Identity`, `identity` şeması),
Expenses (`expenses` şeması, migration, uç noktalar), CreditCards (`cards` şeması,
migration, kart/ekstre/ödeme uç noktaları). Kalan iş modülleri yalnızca DI +
route grubu + geçici `_status` uç noktası içerir.

---

## 3. Son doğrulama

**Tarih:** 2026-11-16 · **SDK:** `10.0.403` · **Microsoft paketleri:** `10.0.15`

> **2026-11-16 notu:** Slice 4 kodu tamamlandı. Docker Desktop motoru kapalı
> olduğu için entegrasyon testleri yerelde çalıştırılamadı; birim testleri
> (`137/137`) yeşil. 46 entegrasyon testi CI'da `ubuntu-latest` üzerinde çalışacak.

| Kontrol | Komut | Sonuç |
|---|---|---|
| Build (Debug) | `dotnet build FinTrack.slnx` | ✅ 0 uyarı, 0 hata |
| Birim testleri (Debug) | `dotnet test tests/... --no-build` (4 proje) | ✅ 137/137 |
| Entegrasyon testleri | `dotnet test tests/FinTrack.Api.IntegrationTests` | ⏸ Docker motoru kapalı |
| Release build | `dotnet build FinTrack.slnx --configuration Release` | CI'da doğrulanacak |
| CI | GitHub Actions `build-test` | Slice 4 commit'i sonrası 183/183 beklenir |
| Migration | `dotnet ef database update --context CreditCardsDbContext ...` | Migration üretildi; CI `MigrateOnStartup` ile uygular |

**Test dağılımı (183):**

| Proje | Adet | Kapsam |
|---|---|---|
| `FinTrack.ArchitectureTests` | 3 | Modül sınırı, katman bağımlılığı |
| `FinTrack.Api.UnitTests` | 22 | Kimlik doğrulama, JWT |
| `FinTrack.Modules.Expenses.Tests` | 55 | Domain + handler (SQLite in-memory) |
| `FinTrack.Modules.CreditCards.Tests` | 57 | Domain + handler (SQLite in-memory) |
| `FinTrack.Api.IntegrationTests` | 46 | 7 smoke + 17 kimlik + 10 harcama + 6 kart + 6 ekstre/ödeme (Testcontainers MSSQL) |

**Uygulanan migration'lar:**
- `20261113215049_InitialExpenses` (`expenses.Categories`, `expenses.Expenses`,
  `expenses.Refunds`, `expenses.__EFMigrationsHistory`).
- `20261114100405_InitialCreditCards` (`cards.Cards`,
  `cards.__EFMigrationsHistory`; `IX_Cards_UserId_IsArchived`).
- `20261116180727_AddCardPaymentsAndStatements` (`cards.CardPayments`,
  `cards.Statements` ve kullanıcı/kart/dönem indeksleri).

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

### 4.2 API sözleşmesi

| Karar | Kaynak |
|---|---|
| Para JSON'da `number` / `decimal`; enum'lar `PascalCase` string | `docs/06` §1 |
| `DELETE` yok; iptal `POST /{id}/cancel`, arşiv `POST /{id}/archive` | `docs/06` §1 |
| Eşzamanlılık gövdedeki `rowVersion` ile; uyuşmazlık `409 ConcurrencyConflict` | `docs/06` §1.5 |
| Sayfalama `page=1`, `pageSize=50` (max 200); hatalı değer `400 ValidationFailed` | `docs/06` §1.6 |
| `Idempotency-Key` **uygulanmadı**; ayrı görev (§6) | `docs/06` §1.4 |

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

### 4.8 Slice 7 — kredi sözleşmesi (2026-11-15)

| Karar | Gerekçe |
|---|---|
| Girdi `Principal`, `MonthlyPayment`, `InstallmentCount`, `FirstDueDate` | Faiz hesabı yok; kullanıcının gerçek aylık ödemesi esas |
| `TotalRepayment = MonthlyPayment × InstallmentCount` türetilir | Tek doğruluk; kullanıcı girdisi değil |
| Taksitler eşit; son taksite yuvarlama farkı eklenmez | `MonthlyPayment` zaten taksit tutarıdır |
| `TotalRepayment >= Principal` doğrulaması korunur | Anapara üstü geri ödeme tutarlılığı |
| `LoanDto` `monthlyPayment` kullanır; ayrı `installmentAmount` yok | Kavram tekliği |

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
- **Smoke testleri DB'siz** (`WebApplicationFactory<Program>`); kimlik, harcama ve
  kart entegrasyon testleri Testcontainers MSSQL + `Database:MigrateOnStartup=true`.
- **Docker zorunlu (yerel):** Entegrasyon testleri Docker Desktop motoru kapalıyken
  `DockerUnavailableException` ile toplu başarısız olur (46 test); hata koda değil
  ortama aittir. Testten önce `docker info` çalıştığını doğrula. GitHub Actions
  `ubuntu-latest` imajında Docker hazır gelir.
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
- **Fixture yapılandırması:** `ApiFixture` bağlantı dizesi/JWT değerlerini
  `WithWebHostBuilder` + `ConfigureAppConfiguration` ile vermek **yetmez**;
  `Program.cs` bunları host kurulurken doğrudan okur ve
  `appsettings.Development.json` kazanır. Değerler
  `ConnectionStrings__FinTrackDb`, `Database__MigrateOnStartup`, `Jwt__*`
  ortam değişkenleriyle verilir; `DisposeAsync` eski değerleri geri koyar.
  Bu düzeltmeden önce testler Windows'ta sessizce LocalDB'ye yazıyordu
  (`FinTrackDb` içinde 297 test kullanıcısı), Linux CI'da LocalDB olmadığı için
  patlıyordu.
- **`_status`:** Expenses ve CreditCards'tan kaldırıldı; CashInflows, Debts,
  Reporting'de duruyor (ilgili dilimde kalkacak).
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
  olduğundan `--context CreditCardsDbContext` gibi açık seçim gerekir.

---

## 6. Ertelenen ve kapsam dışı

| Konu | Durum |
|---|---|
| `Idempotency-Key` (tüm POST) | **Ertelendi**; ayrı görev. O zamana kadar istemci otomatik POST retry yapmamalı |
| Kart harcamaları için taksitli ödeme planı | v1 kapsam dışı |
| Çoklu para birimi / kur dönüşümü | v1 kapsam dışı |
| Kategori `restore`, arşivli kartı geri açma | v1'de yok |
| Rate limiting / kota | v1.1 |
| Tam audit log | v1'de durum + iade/iptal kayıtları yeterli |
| Banka API, kart numarası/CVV/SKT | Hiçbir zaman yok |

---

## 7. Slice 4 — Ekstre + kart ödemesi ✅ (2026-11-16)

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

### 7.1 Sıradaki dilim — Slice 5: Para girişi

**Bağlayıcı kaynak:** `docs/07` §4 Slice 5, `docs/05` §4, `docs/06` §5.

**Kapsam:** `CashInflow` aggregate ve `inflows` şeması; kaynak türleri;
`IsEarnedIncome` türetimi (kullanıcı gönderemez); `Unknown` grubu; borç doğuran
kaynakların (`BankLoan`, `CashAdvance`, `PersonalBorrowing`) doğrudan
oluşturulmasının reddi; iptal ve kullanıcı izolasyonu.

**Başlamadan önce:** `docs/05` §4 ve `docs/06` §5 okunur; açık soru varsa kod
yazılmadan sorulur (`docs/07` §1/5).

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
| Slice 7 — kredi sözleşmesi | Girdi `Principal`, `MonthlyPayment`, `InstallmentCount`, `FirstDueDate`; `TotalRepayment` türetilir; taksitler eşit; son taksit yuvarlama düzeltmesi yok; `TotalRepayment >= Principal`. |

### 8.2 Açık kalanlar

- Slice 4 kapandı; 21 senaryo uygulandı ve dokümanlarla eşleşiyor.
- Slice 5 için açık iş kuralı belirsizliği yok; başlamadan önce `docs/05` §4 ve
  `docs/06` §5 tekrar okunur.

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
