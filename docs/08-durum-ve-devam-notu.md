# FinTrack — Durum ve Devam Notu

**Sürüm:** 1.4
**Tarih:** 2026-11-14
**Bağlı belgeler:** [Yol haritası v1.5](07-yol-haritasi-ve-ajan-gorev-standardi.md) · [API Sözleşmeleri v1.2](06-api-sozlesmeleri.md) · [Teknik Mimari v1.1](03-mimari.md) · [İş Kuralları v1.0](05-is-kurallari.md) · ADR-0001…0008
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
| 4 | Ekstre + kart ödemesi | ⏳ sıradaki |
| 5 | Para girişi | ⏳ |
| 6 | Nakit avans | ⏳ |
| 7 | Kredi + kişisel borç | ⏳ |
| 8 | Raporlama | ⏳ |
| 9 | MAUI istemci | ⏳ |

**Çözüm:** `FinTrack.slnx`, **27 proje**. Depo **git** (`main`); başlangıç
commit'leri alındı, çalışma ağacı temiz. Uzak depo henüz bağlı değil; GitHub
Actions CI (`.github/workflows/ci.yml`) hazır, ilk çalıştırma uzak depo
bağlanınca doğrulanacak.

**Yazılmış modüller:** Identity (`FinTrack.Api/Identity`, `identity` şeması),
Expenses (`expenses` şeması, migration, uç noktalar), CreditCards (`cards` şeması,
migration, uç noktalar). Kalan iş modülleri yalnızca DI + route grubu + geçici
`_status` uç noktası içerir.

---

## 3. Son doğrulama

**Tarih:** 2026-11-14 · **SDK:** `10.0.403` · **Microsoft paketleri:** `10.0.15`

| Kontrol | Komut | Sonuç |
|---|---|---|
| Build (Debug) | `dotnet build FinTrack.slnx` | ✅ 0 uyarı, 0 hata |
| Test (Debug) | `dotnet test FinTrack.slnx` | ✅ 139/139 |
| Build (Release) | `dotnet build FinTrack.slnx --configuration Release` | ✅ 0 uyarı, 0 hata |
| Test (Release) | `dotnet test FinTrack.slnx --no-build --configuration Release` | ✅ 139/139 |
| Migration | `dotnet run --project src/Host/FinTrack.Api -- --migrate` | ✅ LocalDB `FinTrackDb` |
| DB doğrulama | `sqlcmd -S "(localdb)\MSSQLLocalDB" -d FinTrackDb ...` | ✅ `cards` şeması, `Cards` + history, `IX_Cards_UserId_IsArchived` |

**Test dağılımı (139):**

| Proje | Adet | Kapsam |
|---|---|---|
| `FinTrack.ArchitectureTests` | 3 | Modül sınırı, katman bağımlılığı |
| `FinTrack.Api.UnitTests` | 22 | Kimlik doğrulama, JWT |
| `FinTrack.Modules.Expenses.Tests` | 52 | Domain + handler (SQLite in-memory) |
| `FinTrack.Modules.CreditCards.Tests` | 22 | Domain + handler (SQLite in-memory) |
| `FinTrack.Api.IntegrationTests` | 40 | 7 smoke + 17 kimlik + 10 harcama + 6 kart (Testcontainers MSSQL) |

**Uygulanan migration'lar:**
- `20261113215049_InitialExpenses` (`expenses.Categories`, `expenses.Expenses`,
  `expenses.Refunds`, `expenses.__EFMigrationsHistory`).
- `20261114100405_InitialCreditCards` (`cards.Cards`,
  `cards.__EFMigrationsHistory`; `IX_Cards_UserId_IsArchived`).

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
| `ICardExpenseQuery` yalnızca tüm zamanlar toplamı döner; `asOf` yok | Slice 3 kapsamı; `asOf` slice 4'te eklenecek |
| Bakiye `debt = expenses − refunds`; `payments`/`cashAdvances` şimdilik `0` | Formül sözleşmede hazır; terimler slice 4/6'da dolar |
| Yalnızca aktif harcama ve aktif iadeler sayılır | İptal edilen harcamanın iadesi de sayılmaz |
| Bilinmeyen alan reddi yalnızca kart isteklerinde (`JsonUnmappedMemberHandling.Disallow`) | Global JSON davranışı değişmez; kart no/CVV/SKT sızmasın |
| `RouteHandlerOptions.ThrowOnBadRequest = false` | Gövde hatası Development'ta 500 değil, her ortamda `400` |
| `recordedAtUtc` domain `CreatedAtUtc`'ten gelir; istemciden alınmaz | Sunucu zamanı |
| `creditLimit` opsiyonel, `> 0`, en fazla 4 ondalık; borca girmez | docs/06 §6.2 |
| `rowVersion` SQL Server `rowversion`, base64 | Mevcut eşzamanlılık deseni |

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
  `DockerUnavailableException` ile toplu başarısız olur (33/40); hata koda değil
  ortama aittir. Testten önce `docker info` çalıştığını doğrula. GitHub Actions
  `ubuntu-latest` imajında Docker hazır gelir.
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

## 7. Sıradaki dilim — Slice 4: Ekstre + kart ödemesi

**Bağlayıcı kaynak:** `docs/07` §4 Slice 4, `docs/05` §5.3–5.6, `docs/06` §6.4 ve §6.6.

**Kapsam özeti:**
- `CardPayment`: oluşturma, listeleme, iptal; bakiye formülüne `− payments`.
- `Statement`: `PeriodStart`, `PeriodEnd`, `DueDate`, `StatementAmount` manuel;
  `CarriedOverAmount`, `ComputedDebtAtPeriodEnd`, `ReconciliationDifference` türetilir.
- Ekstre/ödeme uç noktaları; `_status` değişmez (kartta yok).
- `ICardExpenseQuery`'ye `asOf` eklenmesi (ekstre anlık borcu için).
- Yeni testler: ödeme gider değildir; fazla ödeme kart alacağı; iptal bakiyeyi
  geri yükseltir; ekstre borcu ikinci kez eklenmez; dönem çakışması reddi.

**Kapsam dışı:** Nakit avans (slice 6), otomatik ekstre içe aktarma, faiz/ücret hesabı.

**Başlamadan önce sorulacaklar (varsayım yapılmayacak):**
1. `computedDebtAtPeriodEnd` hangi ana göre hesaplanır: `periodEnd` gününün sonu
   (`Europe/Istanbul`) mı? `asOf` bu ana mı sabitlenir?
2. `carriedOverAmount` nasıl türetilir? Ödemeler ekstreye açıkça bağlanmadığı için
   önceki ekstre borcundan hangi ödemeler düşülür (tarih sırası/FIFO)?
3. Dönem çakışması kontrolünde iptal edilmiş ekstreler de engel sayılır mı?
4. Kart ödemesi iptali `paidOn`/`cancel` tarih kuralına tabi mi; gerekçe zorunlu mu?
5. `asOf` eklenirken `refunds` ve iptal filtreleri dönem sonuna göre mi
   değerlendirilir (ör. dönem sonundan sonra eklenen iade önceki ekstreyi değiştirir mi)?
6. Bakiye `asOf` parametresi `GET /cards/{id}/balance` sözleşmesine eklenecek mi,
   yoksa yalnızca ekstre içinde mi kullanılacak? (docs/06 §6.3 şu an parametresiz.)

---

## 8. Açık sorular

### 8.1 Yanıtlananlar (2026-11-14)

| Soru | Karar |
|---|---|
| `docs/01` ayrı dosya olarak gerekli mi? | **Gerekli değil.** `docs/_source/` içindeki ham konuşma yeterli; ayrı dosya açılmayacak. |
| Sürüm kontrolü/CI başlatılsın mı? | **Evet.** Git deposu + başlangıç commit'i alındı; GitHub Actions CI eklendi (`.github/workflows/ci.yml`; build + tüm testler). Uzak depo henüz bağlı değil. |
| Nakit avans ve raporlama geçişi | Nakit avans ve kredi **borç** olarak değerlendirilir. **Faiz/KKDF/BSMV hesabı yapılmaz**; aylık ödeme tutarı kullanıcıdan alınır. Slice 7 sözleşmesi (`docs/06` §7.1) bu ilkeye göre netleştirilecek. |

### 8.2 Açık kalanlar

- CI platformu ve uzak depo seçimi: GitHub Actions hazır; **Release build +
  139/139 test yerelde doğrulandı**. Uzak depo (GitHub) bağlanınca ilk CI
  çalıştırması doğrulanacak.
- Slice 7'de kredi sözleşmesi: aylık ödeme tutarı mı, toplam geri ödeme + taksit
  sayısı mı esas alınacak? (Mevcut `docs/06` §7.1 toplam geri ödemeyi kullanır.)

---

## 9. Değişiklik geçmişi

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-11-14 | İlk sürüm; slice 0–2 durumu, kararlar, tuzaklar, slice 3 hazırlığı |
| 1.1 | 2026-11-14 | Oturum protokolüne `docs/09` görev listesi eklendi |
| 1.2 | 2026-11-14 | Slice 3 tamamlandı: durum, test sayıları, kararlar, tuzaklar ve slice 4 hazırlığı güncellendi |
| 1.3 | 2026-11-14 | Açık sorular yanıtlandı: `docs/01` gerekli değil, git başlatıldı, borçlarda faiz yok/aylık ödeme kullanıcıdan |
| 1.4 | 2026-11-14 | Release build + 139/139 test doğrulandı; Docker kapalıyken entegrasyon testi tuzağı eklendi; depo/CI durumu güncellendi |
