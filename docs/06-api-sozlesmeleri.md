# FinTrack — API Sözleşmeleri (v1)

**Sürüm:** 1.4
**Tarih:** 2026-11-15
**Bağlı belgeler:** [Teknik Mimari v1.2](03-mimari.md) · [İş Kuralları v1.2](05-is-kurallari.md) · ADR-0007, ADR-0008
**Hedef okuyucu:** geliştirme ajanları, mobil istemci geliştiricisi

> Bu belge v1 REST API'sinin **davranış ve alan sözleşmesidir**. OpenAPI belgesi
> kodla üretilir; üretilen şema bu belgeyle çelişirse **iş kuralları (05)** ve bu
> belge esas alınır, çelişki düzeltilir. İş kuralı gerektiren yeni bir alan/uç nokta
> eklenmeden önce sorulur.

---

## 1. Genel sözleşme

| Konu | Kural |
|---|---|
| Taban yol | `/api/v1` |
| Taşıma | HTTPS · `application/json; charset=utf-8` |
| Alan adları | `camelCase` (İngilizce) |
| Kimlikler | `Guid` (string) |
| Tarih | `yyyy-MM-dd` (`DateOnly`) |
| Damga | ISO 8601 UTC, `Z` son ekli (`2026-11-14T09:30:00Z`) |
| Para | JSON `number`, `decimal`; v1 yalnızca `TRY` |
| Enum | `PascalCase` string (`Salary`, `CreditCard`, `Cancelled`) |
| Bilinmeyen enum | `400 ValidationFailed`; sessizce yok sayılmaz |
| Boş alan | Opsiyonel alanlar `null` döner; istekte gönderilmemesi veya `null` aynıdır |
| Kimlik doğrulama | `Authorization: Bearer <accessToken>` (auth uç noktaları hariç) |
| Kullanıcı izolasyonu | Başka kullanıcının kaydı **404** döner (varlık sızdırılmaz) |
| Sıralama | v1'de sabit: listelerde en yeni önce; özel `sort` yok |
| Sayfalama | `page` (varsayılan 1), `pageSize` (varsayılan 50, en fazla 200) |
| Hata gövdesi | RFC 9457 `ProblemDetails` + `code`, `traceId`, `errors` |
| İdempotency | `Idempotency-Key` başlığı v1'de desteklenir, zorunlu değildir |
| Eşzamanlılık | Güncelleme/iptal isteklerinde gövdede `rowVersion`; uyuşmazsa `409` |

### 1.1 Para

- Tutarlar `number` olarak taşınır (ör. `1250.5`), para birimi ayrı alandır.
- Sunucu `decimal(19,4)` saklar; **ara toplamda yuvarlama yapmaz**, yalnızca
  gösterimde 2 haneye yuvarlar.
- İstek tutarları en fazla **4 ondalık** taşıyabilir; daha fazlası `400`.
- `TRY` dışındaki `currency` değeri v1'de `400 UnsupportedCurrency` döner.

### 1.2 Ortak hata gövdesi

```json
{
  "type": "https://fintrack.local/errors/business-rule",
  "title": "İş kuralı ihlali",
  "status": 409,
  "detail": "İptal edilmiş harcamaya iade eklenemez.",
  "instance": "/api/v1/expenses/3f1c.../refunds",
  "code": "ExpenseCancelled",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": null
}
```

Doğrulama hatasında `errors` alan adı → mesaj dizisi eşlemesidir:

```json
{
  "type": "https://fintrack.local/errors/validation",
  "title": "Doğrulama hatası",
  "status": 400,
  "code": "ValidationFailed",
  "traceId": "...",
  "errors": {
    "amount": ["Tutar 0'dan büyük olmalıdır."],
    "cardId": ["Kredi kartı ödemesinde kart zorunludur."]
  }
}
```

### 1.3 Hata kodları

| HTTP | `code` | Anlam |
|---|---|---|
| 400 | `ValidationFailed` | Alan/format doğrulaması başarısız |
| 400 | `BusinessRuleViolation` | İş kuralı gereği istek reddedildi |
| 400 | `UnsupportedCurrency` | v1'de yalnızca `TRY` |
| 400 | `DebtSourceRequiresOwnerEndpoint` | Borç doğuran kaynak yanlış uç noktadan yaratılmak istendi |
| 401 | `Unauthorized` | Token yok/geçersiz/süresi dolmuş |
| 401 | `InvalidCredentials` | E-posta veya parola hatalı |
| 401 | `InvalidRefreshToken` | Refresh token geçersiz/süresi dolmuş |
| 404 | `NotFound` | Kayıt yok veya kullanıcıya ait değil |
| 409 | `ConcurrencyConflict` | `rowVersion` uyuşmuyor |
| 409 | `StateConflict` | Kayıt mevcut durumunda bu işlemi kabul etmiyor |
| 409 | `EmailAlreadyRegistered` | E-posta zaten kayıtlı |
| 409 | `IdempotencyKeyReused` | Aynı anahtar farklı gövdeyle kullanıldı |
| 409 | `IdempotencyInProgress` | Aynı anahtarlı istek hâlâ işleniyor |
| 500 | `InternalError` | Beklenmeyen hata; iç detay dönmez |

### 1.4 İdempotency

- Yalnızca `POST` isteklerinde `Idempotency-Key` (UUID) kabul edilir.
- Kapsam: kullanıcı + method + path + anahtar. Saklama süresi **24 saat**.
- Aynı anahtar + aynı gövde → ilk yanıt (durum kodu ve gövde) tekrar döner;
  yeni kayıt oluşmaz.
- Aynı anahtar + farklı gövde → `409 IdempotencyKeyReused`.
- İlk istek hâlâ işleniyorsa → `409 IdempotencyInProgress`.
- v1.1'de tüm `POST` uç noktalarında **zorunlu** hale getirilmesi planlanır
  (doküman 05 §2.7).
- **Uygulama notu (2026-11-14):** Slice 2 (harcama + kategori) kapsamında
  `Idempotency-Key` uygulanmadı. Davranış sözleşmesi geçerlidir; destek ayrı bir
  görevde eklenecektir. O zamana kadar istemci aynı `POST` isteğini otomatik
  yeniden denememelidir.

### 1.5 Eşzamanlılık

- Aggregate kökü yanıtlarında `rowVersion` (base64 string) bulunur.
- `PUT` ve iptal/arşiv istekleri, hedef kaydın `rowVersion` değerini gövdede taşır.
- Uyuşmazlıkta `409 ConcurrencyConflict` döner; istemci kaydı yeniden yükler.
- Alt kayıt iptallerinde (iade, ödeme, geri ödeme) **alt kaydın** `rowVersion`'ı
  gönderilir.

### 1.6 Sayfalama yanıtı

```json
{
  "items": [],
  "page": 1,
  "pageSize": 50,
  "totalCount": 0,
  "totalPages": 0
}
```

Listelerde `status = Cancelled` açıkça verilirse iptal kayıtlar döner; aksi
durumda `includeCancelled` (varsayılan `false`) belirler.

### 1.7 İptal ve arşiv

- Finansal kayıtlar **hard delete edilmez**; `DELETE` uç noktası **yoktur**.
- İptal: `POST /{kaynak}/{id}/cancel`, gövde `{ "reason": "...", "rowVersion": "..." }`.
- Kart ve kategori silinmez; `archive` uç noktası kullanılır.
- İptal edilen kayıt listelerde `Cancelled` durumuyla görünür, toplamlardan çıkar.

### 1.8 Ortak istek gövdeleri

`CancelRequest`:

| Alan | Tip | Kural |
|---|---|---|
| `reason` | string | Zorunlu, 1–500 karakter |
| `rowVersion` | string | Zorunlu; hedef kaydın sürümü |

`ArchiveRequest`:

| Alan | Tip | Kural |
|---|---|---|
| `rowVersion` | string | Zorunlu |

---

## 2. Kimlik doğrulama

### 2.1 `POST /auth/register`

İstek:

```json
{
  "email": "user@example.com",
  "password": "GucluParola1",
  "preferredTimeZone": "Europe/Istanbul"
}
```

| Alan | Tip | Kural |
|---|---|---|
| `email` | string | Zorunlu; normalize edilir, benzersiz |
| `password` | string | 8–128 karakter; en az bir harf ve bir rakam |
| `preferredTimeZone` | string? | IANA kimliği; varsayılan `Europe/Istanbul` |

Yanıt `201 Created`:

```json
{
  "userId": "8a1f6f2e-...",
  "email": "user@example.com",
  "preferredTimeZone": "Europe/Istanbul",
  "accessToken": "eyJ...",
  "refreshToken": "4f2c...",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

Hatalar: `400 ValidationFailed`, `409 EmailAlreadyRegistered`.

### 2.2 `POST /auth/login`

İstek: `{ "email": "...", "password": "..." }`
Yanıt `200`: `AuthResponse` (2.1 ile aynı gövde).
Hatalar: `401 InvalidCredentials`.

### 2.3 `POST /auth/refresh`

İstek: `{ "refreshToken": "..." }`
Yanıt `200`: yeni `AuthResponse`; **refresh token rotating**'dir, eskisi geçersiz olur.
Hatalar: `401 InvalidRefreshToken`.

### 2.4 `POST /auth/logout`

İstek: `{ "refreshToken": "..." }` → `204 No Content`.
Token zaten geçersizse de `204` döner (idempotent).

### 2.5 `GET /auth/me`

Yanıt `200`:

```json
{
  "userId": "8a1f6f2e-...",
  "email": "user@example.com",
  "preferredTimeZone": "Europe/Istanbul",
  "rowVersion": "AAAAAAAAB9E="
}
```

### 2.6 `PUT /auth/me`

İstek:

```json
{
  "preferredTimeZone": "Europe/Istanbul",
  "rowVersion": "AAAAAAAAB9E="
}
```

Yanıt `200`: güncel profil. IANA kimliği geçersizse `400`.

---

## 3. Kategoriler (Expenses modülü)

### 3.1 Uç noktalar

| Method | Yol | Açıklama |
|---|---|---|
| GET | `/categories?includeArchived=false` | Kullanıcının kategorileri (sayfalanmaz) |
| POST | `/categories` | Kategori oluştur |
| PUT | `/categories/{id}` | Ad/ikon güncelle |
| POST | `/categories/{id}/archive` | Kategoriyi arşivle |

### 3.2 `CategoryDto`

```json
{
  "id": "c1a2...",
  "name": "Market",
  "iconKey": "cart",
  "isArchived": false,
  "rowVersion": "AAAAAAAAB9E="
}
```

- Ad, kullanıcıda büyük/küçük harf duyarsız benzersizdir; çakışma `409 StateConflict`.
- Harcaması olan kategori silinemez; arşivlenir. Arşivli kategori geçmişte görünür,
  yeni harcamada seçilemez.

### 3.3 Oluşturma/güncelleme

`POST /categories` → `201` + `CategoryDto`:

```json
{ "name": "Market", "iconKey": "cart" }
```

`PUT /categories/{id}` → `200` + `CategoryDto`:

```json
{ "name": "Gıda", "iconKey": "cart", "rowVersion": "AAAAAAAAB9E=" }
```

`POST /categories/{id}/archive` → `204`; gövde `ArchiveRequest`.

---

## 4. Harcamalar (Expenses)

### 4.1 Uç noktalar

| Method | Yol | Açıklama |
|---|---|---|
| GET | `/expenses` | Filtreli, sayfalı liste |
| POST | `/expenses` | Harcama oluştur (kart alışverişi dahil) |
| GET | `/expenses/{id}` | Tek harcama |
| PUT | `/expenses/{id}` | Harcamayı güncelle |
| POST | `/expenses/{id}/cancel` | Harcamayı iptal et |
| GET | `/expenses/{id}/refunds` | İadeleri listele |
| POST | `/expenses/{id}/refunds` | İade ekle |
| POST | `/expenses/{id}/refunds/{refundId}/cancel` | İadeyi iptal et |

### 4.2 Liste filtreleri

| Parametre | Tip | Kural |
|---|---|---|
| `from` | date? | `occurredOn >= from` |
| `to` | date? | `occurredOn <= to` |
| `categoryId` | guid? | Kategori filtresi |
| `cardId` | guid? | **Kart alışverişleri** için filtre |
| `paymentMethod` | enum? | `Cash`, `DebitCard`, `CreditCard`, `BankTransfer`, `Other` |
| `status` | enum? | `Active`, `PartiallyRefunded`, `Refunded`, `Cancelled` |
| `includeCancelled` | bool? | Varsayılan `false`; `true` ise iptaller de döner |
| `page`, `pageSize` | int? | Sayfalama |

Yanıt `200`: `PagedResult<ExpenseDto>`.

### 4.3 `ExpenseDto`

```json
{
  "id": "3f1c...",
  "amount": 1250.00,
  "currency": "TRY",
  "occurredOn": "2026-11-13",
  "categoryId": "c1a2...",
  "categoryName": "Market",
  "description": "Haftalık alışveriş",
  "paymentMethod": "CreditCard",
  "cardId": "9b7d...",
  "status": "Active",
  "refundedAmount": 0,
  "netAmount": 1250.00,
  "recordedAtUtc": "2026-11-13T18:20:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

- `status` ve `refundedAmount`/`netAmount` **türetilir**; istemci set edemez.
- `cardId` yalnızca `paymentMethod = CreditCard` iken doludur.
- `recordedAtUtc` değişmez.

### 4.4 `POST /expenses`

```json
{
  "amount": 1250.00,
  "currency": "TRY",
  "occurredOn": "2026-11-13",
  "categoryId": "c1a2...",
  "description": "Haftalık alışveriş",
  "paymentMethod": "CreditCard",
  "cardId": "9b7d..."
}
```

| Alan | Tip | Kural |
|---|---|---|
| `amount` | number | `> 0`, en fazla 4 ondalık |
| `currency` | string? | Varsayılan `TRY`; başka değer `400` |
| `occurredOn` | date | Bugün veya geçmiş |
| `categoryId` | guid | Kullanıcıya ait, arşivsiz kategori |
| `description` | string? | En fazla 500 karakter |
| `paymentMethod` | enum | Zorunlu |
| `cardId` | guid? | `CreditCard` ise zorunlu; diğerlerinde `null`/yok |

Kurallar:
- `cardId` verilirse kart kullanıcıya ait ve `Active` olmalı; arşivli karta yazılamaz.
- **Kart alışverişi ayrı kayıt oluşturmaz**; bu uç nokta tek kaydı yaratır (ADR-0008).
- Yanıt `201 Created`, `Location: /api/v1/expenses/{id}`, gövde `ExpenseDto`.

### 4.5 `PUT /expenses/{id}`

Gövde: `amount`, `currency?`, `occurredOn`, `categoryId`, `description?`,
`paymentMethod`, `cardId?`, `rowVersion`.

- `recordedAtUtc` gönderilmez; gönderilirse yok sayılmaz → `400`.
- İptal edilmiş harcama güncellenemez → `409 StateConflict`.
- Tutar, aktif iadeler toplamının altına düşürülemez → `400 BusinessRuleViolation`.
- `paymentMethod` `CreditCard` dışına çıkarsa `cardId` temizlenir; `CreditCard`
  yapılırsa geçerli `cardId` zorunludur.
- İade tarihleri yeni `occurredOn` ile tutarsız kalırsa `400`.

Yanıt `200` + `ExpenseDto`.

### 4.6 İptal

`POST /expenses/{id}/cancel` gövde `CancelRequest` → `204`.

- Mevcut aktif iade varsa harcama iptal edilemez → `409 StateConflict`;
  önce iadeler iptal edilir.
- İptal edilen harcama toplamlardan çıkar, listede `Cancelled` görünür.

### 4.7 İadeler

`GET /expenses/{id}/refunds` → `200` + `RefundDto[]`.

`POST /expenses/{id}/refunds`:

```json
{
  "amount": 200.00,
  "refundedOn": "2026-11-14",
  "description": "Kısmi iade"
}
```

| Alan | Tip | Kural |
|---|---|---|
| `amount` | number | `> 0`; aktif iadeler toplamı harcamayı aşamaz |
| `refundedOn` | date | `>= expense.occurredOn` ve `<= bugün` |
| `description` | string? | En fazla 500 karakter |

Yanıt `201` + `RefundDto`.

`RefundDto`:

```json
{
  "id": "r5e6...",
  "expenseId": "3f1c...",
  "amount": 200.00,
  "currency": "TRY",
  "refundedOn": "2026-11-14",
  "description": "Kısmi iade",
  "status": "Active",
  "recordedAtUtc": "2026-11-14T10:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

- İade, harcamayı silmez; net tutarı azaltır ve **gelir oluşturmaz**.
- İade, orijinal harcamanın kategorisinde negatif gider olarak raporlanır.
- `POST /expenses/{id}/refunds/{refundId}/cancel` gövde `CancelRequest` → `204`;
  iptal edilen iade net tutara geri eklenir.
- İptal edilmiş harcamaya yeni iade eklenemez → `409 StateConflict`.

---

## 5. Para girişleri (CashInflows)

### 5.1 Uç noktalar

| Method | Yol | Açıklama |
|---|---|---|
| GET | `/cash-inflows` | Filtreli, sayfalı liste |
| POST | `/cash-inflows` | Gelir/diğer para girişi oluştur |
| GET | `/cash-inflows/{id}` | Tek kayıt |
| PUT | `/cash-inflows/{id}` | Güncelle |
| POST | `/cash-inflows/{id}/cancel` | İptal et |

> `BankLoan`, `CashAdvance` ve `PersonalBorrowing` kaynaklı girişler bu uç
> noktadan **oluşturulamaz**; sahibi olan borç/kart uç noktası kullanılır
> (doküman 05 §4.3). Aksi halde `400 DebtSourceRequiresOwnerEndpoint`.

### 5.2 Liste filtreleri

`from`, `to` (`occurredOn`), `source`, `status` (`Active`, `Cancelled`),
`includeCancelled`, `page`, `pageSize`.

### 5.3 `CashInflowDto`

```json
{
  "id": "7d8e...",
  "amount": 80000.00,
  "currency": "TRY",
  "occurredOn": "2026-11-01",
  "source": "Salary",
  "isEarnedIncome": true,
  "description": "Kasım maaşı",
  "status": "Active",
  "debtLink": null,
  "recordedAtUtc": "2026-11-01T09:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

`debtLink` borç doğuran girişlerde:

```json
{ "type": "CashAdvance", "id": "a1b2..." }
```

| `type` | Sahip |
|---|---|
| `Loan` | Debts |
| `CashAdvance` | CreditCards |
| `PersonalDebt` | Debts |

### 5.4 Kaynak ve gelir niteliği

| `source` | `isEarnedIncome` | Oluşturma yolu |
|---|---|---|
| `Salary` | `true` | `POST /cash-inflows` |
| `BusinessIncome` | `true` | `POST /cash-inflows` |
| `BankLoan` | `false` | `POST /loans` (aynı işlemde) |
| `CashAdvance` | `false` | `POST /cards/{id}/cash-advances` |
| `PersonalBorrowing` | `false` | `POST /personal-debts` |
| `AssetSale` | `false` | `POST /cash-inflows` |
| `Unknown` | `null` | `POST /cash-inflows` |

- `isEarnedIncome` istemci tarafından gönderilemez; gönderilirse `400`.
- `Unknown` aylık özette **ayrı "Belirsiz" grubunda** gösterilir, gelire katılmaz.
- `AssetSale` için maliyet/kazanç hesabı yoktur.

### 5.5 Güncelleme ve iptal

`PUT /cash-inflows/{id}` gövdesi: `amount`, `currency?`, `occurredOn`, `source`,
`description?`, `rowVersion`. Yalnızca borç bağlantısı olmayan kayıtlar güncellenebilir;
borç bağlantılı kayıt `409 StateConflict` döner. Kaynak yine borç doğuran türlere
çevrilemez.

`POST /cash-inflows/{id}/cancel` gövde `CancelRequest`.

- Borç bağlantılı giriş **doğrudan iptal edilemez**; sahibi borç kaydı iptal edilir
  → `409 StateConflict`.
- `Salary`, `BusinessIncome`, `AssetSale`, `Unknown` iptal edilebilir.

---

## 6. Kredi kartları (CreditCards)

### 6.1 Uç noktalar

| Method | Yol | Açıklama |
|---|---|---|
| GET | `/cards` | Kart listesi (`status` filtresi) |
| POST | `/cards` | Kart oluştur |
| GET | `/cards/{id}` | Kart detayı |
| PUT | `/cards/{id}` | Kart tanımını güncelle |
| POST | `/cards/{id}/archive` | Kartı arşivle |
| GET | `/cards/{id}/balance` | Hesaplanan kart borcu |
| GET | `/cards/{id}/statements` | Ekstreler |
| POST | `/cards/{id}/statements` | Ekstre ekle |
| GET | `/cards/{id}/statements/{statementId}` | Tek ekstre |
| POST | `/cards/{id}/statements/{statementId}/cancel` | Ekstreyi iptal et |
| GET | `/cards/{id}/payments` | Kart ödemeleri |
| POST | `/cards/{id}/payments` | Kart ödemesi ekle |
| POST | `/cards/{id}/payments/{paymentId}/cancel` | Ödemeyi iptal et |
| GET | `/cards/{id}/cash-advances` | Nakit avanslar |
| POST | `/cards/{id}/cash-advances` | Nakit avans ekle |
| POST | `/cards/{id}/cash-advances/{cashAdvanceId}/cancel` | Nakit avansı iptal et |

> **Kart alışverişi için uç nokta yoktur.** Kart alışverişleri `POST /expenses`
> ile `paymentMethod = CreditCard` + `cardId` olarak oluşturulur; listesi
> `GET /expenses?cardId=...` ile alınır (ADR-0008).

> Slice 3 uygulaması: `GET /cards` varsayılan olarak yalnızca `Active` kartları
> döner; arşivliler `?status=Archived` ile listelenir. Arşivli kartın detayı ve
> geçmiş bakiyesi okunabilir.

### 6.2 `CardDto`

```json
{
  "id": "9b7d...",
  "bankName": "Örnek Bank",
  "displayName": "Platinum",
  "logoKey": "ornek-bank",
  "creditLimit": 50000.00,
  "statementDay": 25,
  "paymentDueDay": 5,
  "status": "Active",
  "recordedAtUtc": "2026-10-01T12:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

| Alan | Kural |
|---|---|
| `bankName` | Zorunlu |
| `displayName` | Zorunlu |
| `logoKey` | Opsiyonel |
| `creditLimit` | Opsiyonel; bilgi amaçlı, borca girmez |
| `statementDay`, `paymentDueDay` | Opsiyonel, 1–31 |
| `status` | `Active`, `Archived` |

Oluşturma/güncelleme: `bankName`, `displayName`, `logoKey?`, `creditLimit?`,
`statementDay?`, `paymentDueDay?` (+ güncellemede `rowVersion`).

- Gerçek kart numarası, CVV, son kullanma tarihi **kabul edilmez**; bilinmeyen
  alan gönderilirse `400`. Slice 3'te bu kural yalnızca kart istek gövdelerine
  uygulanır (`[JsonUnmappedMemberHandling(Disallow)]`); global JSON davranışı
  değiştirilmez. Gövde bağlama hataları tüm ortamlarda `400` döner.
- Arşivli karta yeni harcama/ödeme/nakit avans/ekstre eklenemez.
- `POST /cards/{id}/archive` → `204`, gövde `ArchiveRequest`.

### 6.3 `GET /cards/{id}/balance?asOf=YYYY-MM-DD`

`asOf` opsiyoneldir; verilmezse kullanıcının bugünkü tarihi kullanılır. Değer
kullanıcının saat diliminde bir `DateOnly`'dir; ilgili günün sonu dahildir.

Yanıt `200`:

```json
{
  "cardId": "9b7d...",
  "asOfDate": "2026-11-14",
  "currency": "TRY",
  "debt": 4200.00,
  "isInCredit": false,
  "breakdown": {
    "expenses": 12000.00,
    "refunds": 800.00,
    "cashAdvances": 3000.00,
    "payments": 10000.00
  }
}
```

Hesap:

```
debt = expenses − refunds + cashAdvances − payments
```

- Kesit: `OccurredOn` / `RefundedOn` / `PaidOn` / `TakenOn` tarihi `asOfDate`'e
  **dahil** edilir; sonraki günler hariçtir.
- İptal: kaydın `CancelledAtUtc` UTC zamanı kullanıcının saat dilimine çevrilir.
  Yerel iptal tarihi `asOfDate`'ten **önce veya eşit** ise kayıt kesitte iptal
  sayılır ve toplama girmez; **sonra** ise kesitte aktif sayılır. Aynı kural
  harcama, iade ve kart ödemesi için geçerlidir.
- `debt` negatif olabilir; `isInCredit = true` ise kart lehine bakiye vardır.
- Devreden ekstre borcu ayrıca eklenmez.
- Harcama/iade verisi Expenses'tan kesitli `ICardExpenseQuery` ile okunur.
- Slice 4'te `cashAdvances` henüz yoktur; `0` kabul edilir (slice 6'da eklenir).
  `payments` slice 4 ile dolar.
- Geçersiz `asOf` formatı `400 ValidationFailed` döner. Gelecek tarihli `asOf`
  (kullanıcının bugününden sonrası) da `400 ValidationFailed` döner; bugüne
  kırpılmaz.

### 6.4 Kart ödemesi

`POST /cards/{id}/payments`:

```json
{
  "amount": 5000.00,
  "currency": "TRY",
  "paidOn": "2026-11-10",
  "note": "Ekstre ödemesi"
}
```

Yanıt `201` + `CardPaymentDto`:

```json
{
  "id": "p3a4...",
  "cardId": "9b7d...",
  "amount": 5000.00,
  "currency": "TRY",
  "paidOn": "2026-11-10",
  "note": "Ekstre ödemesi",
  "status": "Active",
  "recordedAtUtc": "2026-11-10T14:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

- `amount > 0`, `paidOn <= bugün`, kart `Active` olmalı.
- Kart ödemesi **gider değildir**; ikinci kez gider oluşturmaz.
- Borcu aşan ödeme kabul edilir; bakiye negatife düşer (kart alacağı).
- `paidOn` finansal gerçekleşme tarihidir. İptal zamanı `cancelledAtUtc` ayrı
  bir sistem zamanıdır (sunucuda UTC); iptal `paidOn` değerini değiştirmez.
- `GET /cards/{id}/payments?status=&includeCancelled=false&page=&pageSize=` →
  `200` + `PagedResult<CardPaymentDto>`. Varsayılan yalnızca aktif ödemelerdir;
  `status=Cancelled` iptalleri döner.
- `POST /cards/{id}/payments/{paymentId}/cancel` gövde `CancelRequest` → `204`;
  iptal gerekçesi zorunludur ve iptal borcu geri yükseltir. Güncel bakiyede iptal
  edilmiş ödeme sayılmaz. Kesitli hesapta `CancelledAtUtc` UTC zamanı kullanıcı
  yerel tarihine çevrilir; yerel iptal tarihi kesitle aynı gün veya önceyse ödeme
  kesitte iptal, sonraysa aktif sayılır (docs/05 §5.3–5.4).

### 6.5 Nakit avans

`POST /cards/{id}/cash-advances`:

```json
{
  "amount": 3000.00,
  "currency": "TRY",
  "takenOn": "2026-11-05",
  "description": "Acil nakit"
}
```

Yanıt `201` + `CashAdvanceDto`:

```json
{
  "id": "a1b2...",
  "cardId": "9b7d...",
  "amount": 3000.00,
  "currency": "TRY",
  "takenOn": "2026-11-05",
  "description": "Acil nakit",
  "status": "Active",
  "linkedCashInflowId": "7d8e...",
  "recordedAtUtc": "2026-11-05T11:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

- Aynı işlemde `CashInflow` (`source = CashAdvance`, `isEarnedIncome = false`)
  oluşturulur; yarım kayıt bırakılmaz.
- Nakit avans **gider değildir**; kart borcunun bileşenidir ve toplam borçta
  **bir kez** sayılır.
- Geri ödemesi ayrı bir kayıt türü değildir; **kart ödemesi** olarak girilir.
- `POST /cards/{id}/cash-advances/{cashAdvanceId}/cancel` gövde `CancelRequest`
  → `204`; bağlı para girişi de iptal edilir.

### 6.6 Ekstre

`GET /cards/{id}/statements?status=&includeCancelled=false&page=&pageSize=` →
`200` + `PagedResult<StatementDto>`. Varsayılan yalnızca aktif ekstrelerdir;
`status=Cancelled` iptalleri döner. `GET /cards/{id}/statements/{statementId}`
tek ekstreyi döner.

`POST /cards/{id}/statements`:

```json
{
  "periodStart": "2026-10-26",
  "periodEnd": "2026-11-25",
  "dueDate": "2026-12-05",
  "statementAmount": 6500.00,
  "currency": "TRY"
}
```

| Alan | Kural |
|---|---|
| `periodStart` | Zorunlu; `<= periodEnd` |
| `periodEnd` | Zorunlu; aynı kartta yalnızca **aktif** ekstre dönemleriyle çakışamaz |
| `dueDate` | Zorunlu; `>= periodEnd` |
| `statementAmount` | Zorunlu; `>= 0` |
| `currency` | Opsiyonel; varsayılan `TRY` |

Yanıt `201` + `StatementDto`:

```json
{
  "id": "s9t0...",
  "cardId": "9b7d...",
  "periodStart": "2026-10-26",
  "periodEnd": "2026-11-25",
  "dueDate": "2026-12-05",
  "statementAmount": 6500.00,
  "currency": "TRY",
  "carriedOverAmount": 0.00,
  "computedDebtAtPeriodEnd": 6450.00,
  "reconciliationDifference": 50.00,
  "status": "Active",
  "recordedAtUtc": "2026-11-25T20:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

- `carriedOverAmount`, `computedDebtAtPeriodEnd`, `reconciliationDifference`
  **türetilir**; istemci gönderemez. Okuma anında güncel kayıtlardan hesaplanır,
  saklanmaz; sonradan girilen geçmiş tarihli iade/ödeme veya iptal bu alanları
  günceller.
- `carriedOverAmount`: aynı kartta mevcut `periodEnd`'den küçük `periodEnd`'e
  sahip **aktif** ekstreler arasından en büyük `periodEnd`'li ekstreye göre
  hesaplanır; yoksa `0`. Formül ve sınırlar docs/05 §5.6'dadır.
- `computedDebtAtPeriodEnd`: `periodEnd` gününün sonu (kullanıcı saat diliminde)
  kesitiyle hesaplanan kart borcudur; docs/05 §5.3'teki kesit/iptal kuralları
  geçerlidir.
- `statementAmount` kart borcuna **eklenmez**, gider oluşturmaz.
- Fark otomatik olarak gider/faiz/borç kaydı oluşturmaz; yalnızca gösterilir.
- `POST /cards/{id}/statements/{statementId}/cancel` gövde `CancelRequest` → `204`;
  iptal borç hesabını değiştirmez, dönemi çakışma kontrolünde serbest bırakır ve
  hard delete yapılmaz.

---

## 7. Borçlar (Debts)

### 7.1 Krediler

| Method | Yol | Açıklama |
|---|---|---|
| GET | `/loans?status=` | Sayfalı kredi listesi |
| POST | `/loans` | Kredi oluştur (bağlı para girişiyle) |
| GET | `/loans/{id}` | Kredi + taksit planı |
| POST | `/loans/{id}/cancel` | Krediyi iptal et |
| POST | `/loans/{id}/installments/{number}/pay` | Taksit öde |
| POST | `/loans/{id}/installments/{number}/payment/cancel` | Taksit ödemesini iptal et |

`POST /loans`:

```json
{
  "principal": 30000.00,
  "monthlyPayment": 3000.00,
  "installmentCount": 12,
  "firstDueDate": "2026-12-01",
  "bankName": "Örnek Bank",
  "description": "İhtiyaç kredisi",
  "currency": "TRY"
}
```

| Alan | Kural |
|---|---|
| `principal` | `> 0`; ele geçen tutar |
| `monthlyPayment` | `> 0`; aylık taksit tutarı; en fazla 4 ondalık |
| `installmentCount` | 1–120 |
| `firstDueDate` | Kredi tarihinden önce olamaz |
| `bankName`, `description` | Opsiyonel |
| `currency` | Opsiyonel; varsayılan `TRY` |

- `totalRepayment` istemci girdisi değildir; `monthlyPayment × installmentCount`
  ile türetilir ve `>= principal` olmalıdır. Gönderilirse `400`.
- Taksit tutarları eşittir; son taksite otomatik yuvarlama farkı eklenmez.
  Yuvarlama yalnızca gösterimde 2 haneye yapılır.
- Yanıt `201` + `LoanDto`. Aynı işlemde `CashInflow` (`source = BankLoan`,
  `amount = principal`) oluşturulur; para girişi **kazanılmış gelir değildir**.

`LoanDto`:

```json
{
  "id": "l1o2...",
  "principal": 30000.00,
  "monthlyPayment": 3000.00,
  "installmentCount": 12,
  "totalRepayment": 36000.00,
  "firstDueDate": "2026-12-01",
  "bankName": "Örnek Bank",
  "description": "İhtiyaç kredisi",
  "currency": "TRY",
  "status": "Active",
  "remainingAmount": 36000.00,
  "recordedAtUtc": "2026-11-14T12:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "installments": [
    {
      "number": 1,
      "dueDate": "2026-12-01",
      "amount": 3000.00,
      "status": "Planned",
      "paidOn": null,
      "payment": null
    }
  ]
}
```

- `totalRepayment` yanıtta türetilmiş bilgi olarak gösterilir; `LoanDto`'da ayrı
  `installmentAmount` alanı yoktur. Taksit tutarı `monthlyPayment` ve
  `installments[].amount` ile ifade edilir.
- `remainingAmount = totalRepayment − Σ(Paid taksit tutarları)`.

`InstallmentDto.payment` (ödenmişse):

```json
{
  "id": "ip3a...",
  "amount": 3000.00,
  "paidOn": "2026-11-30",
  "status": "Active",
  "recordedAtUtc": "2026-11-30T10:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

Taksit ödeme:

`POST /loans/{id}/installments/{number}/pay`

```json
{ "paidOn": "2026-11-30", "rowVersion": "AAAAAAAAB9E=" }
```

- Taksitler **sırayla** ödenir; önceki ödenmeden sonraki ödenemez → `409`.
- Tam taksit tutarı ödenir; kısmi ödeme v1'de yoktur.
- `paidOn <= bugün`; bir taksitte tek aktif ödeme olabilir.
- `rowVersion` kredi aggregate'inin sürümüdür.
- Taksit ödemesi **gider değildir**; kalan borcu azaltır.

Taksit ödemesi iptali:

`POST /loans/{id}/installments/{number}/payment/cancel` gövde `CancelRequest`
(`rowVersion` = ödeme kaydının sürümü) → `204`. Taksit tekrar `Planned` olur.

Kredi iptali:

`POST /loans/{id}/cancel` gövde `CancelRequest`.

- Bağlı `CashInflow` da iptal edilir.
- Ödenmiş taksit varsa kredi iptal edilemez → `409 StateConflict`; önce taksit
  ödemeleri iptal edilir (doküman 05 §6.6'daki kuralın krediye uygulanışı).
- İptal edilen kredi raporlardan çıkar.

### 7.2 Kişisel borçlar

| Method | Yol | Açıklama |
|---|---|---|
| GET | `/personal-debts?status=` | Sayfalı liste |
| POST | `/personal-debts` | Borç oluştur (bağlı para girişiyle) |
| GET | `/personal-debts/{id}` | Borç + geri ödemeler |
| POST | `/personal-debts/{id}/cancel` | Borcu iptal et |
| GET | `/personal-debts/{id}/repayments` | Geri ödemeler |
| POST | `/personal-debts/{id}/repayments` | Geri ödeme ekle |
| POST | `/personal-debts/{id}/repayments/{repaymentId}/cancel` | Geri ödemeyi iptal et |

`POST /personal-debts`:

```json
{
  "counterpartyName": "Ahmet",
  "principal": 5000.00,
  "borrowedOn": "2026-11-02",
  "dueDate": "2026-12-02",
  "description": "Acil ihtiyaç",
  "currency": "TRY"
}
```

Yanıt `201` + `PersonalDebtDto`:

```json
{
  "id": "pd1e...",
  "counterpartyName": "Ahmet",
  "principal": 5000.00,
  "remainingAmount": 5000.00,
  "borrowedOn": "2026-11-02",
  "dueDate": "2026-12-02",
  "description": "Acil ihtiyaç",
  "currency": "TRY",
  "status": "Active",
  "recordedAtUtc": "2026-11-02T10:00:00Z",
  "rowVersion": "AAAAAAAAB9E=",
  "repayments": []
}
```

- Aynı işlemde `CashInflow` (`source = PersonalBorrowing`, `isEarnedIncome = false`)
  oluşturulur.
- `remainingAmount` türetilir; istemci set edemez.

Geri ödeme:

`POST /personal-debts/{id}/repayments`

```json
{ "amount": 1000.00, "paidOn": "2026-11-20", "note": "İlk taksit" }
```

Yanıt `201` + `RepaymentDto`:

```json
{
  "id": "rp4a...",
  "personalDebtId": "pd1e...",
  "amount": 1000.00,
  "currency": "TRY",
  "paidOn": "2026-11-20",
  "note": "İlk taksit",
  "status": "Active",
  "recordedAtUtc": "2026-11-20T18:00:00Z",
  "rowVersion": "AAAAAAAAB9E="
}
```

- `amount > 0`, `paidOn <= bugün`; aktif geri ödemeler toplamı `principal`'ı aşamaz.
- `remainingAmount = principal − Σ(aktif geri ödemeler)`.
- `remainingAmount = 0` → borç `Closed`.
- Geri ödeme iptali: `POST .../repayments/{repaymentId}/cancel` gövde
  `CancelRequest`; kalan tutar geri yükselir.
- Aktif geri ödeme varken borç iptal edilemez → `409`; önce geri ödemeler iptal edilir.

---

## 8. Raporlama (Reporting)

Raporlama **salt okunurdur**; hiçbir kaydın sahibi değildir. Tüm toplamlar
kullanıcının saat dilimine göre hesaplanır (varsayılan `Europe/Istanbul`).

### 8.1 `GET /reports/monthly?year=2026&month=11`

| Parametre | Kural |
|---|---|
| `year` | 2000–2100 |
| `month` | 1–12 |

Yanıt `200`:

```json
{
  "period": {
    "year": 2026,
    "month": 11,
    "from": "2026-11-01",
    "to": "2026-11-30",
    "timeZone": "Europe/Istanbul"
  },
  "currency": "TRY",
  "earnedIncome": 80000.00,
  "otherInflows": {
    "total": 35000.00,
    "items": [
      { "source": "BankLoan", "amount": 30000.00 },
      { "source": "CashAdvance", "amount": 5000.00 }
    ]
  },
  "unknownInflows": {
    "total": 1000.00,
    "count": 1
  },
  "expense": {
    "gross": 50000.00,
    "refunds": 2000.00,
    "net": 48000.00
  },
  "incomeExpenseDifference": 32000.00,
  "isBankBalance": false,
  "disclaimer": "Gelir−gider farkı banka bakiyesi değildir.",
  "categories": [
    {
      "categoryId": "c1a2...",
      "categoryName": "Market",
      "netAmount": 18000.00
    }
  ]
}
```

Kurallar:
- `earnedIncome` yalnızca aktif, `isEarnedIncome = true` girişlerdir.
- `otherInflows` bilinen ve kazanılmış olmayan kaynaklardır; gelire katılmaz.
- `unknownInflows` ayrı gruptur; gelire ve `otherInflows`'a karışmaz.
- `expense.net = gross − refunds`.
- İade, **gerçekleştiği dönemde** gideri azaltır; orijinal dönem geriye dönük
  değiştirilmez.
- `incomeExpenseDifference = earnedIncome − expense.net`.
- `isBankBalance` her zaman `false`; rapor banka bakiyesi iddiası taşımaz.
- Kategori kırılımı net tutarları içerir; iade negatif gider olarak yansır.

### 8.2 `GET /reports/debt-summary`

Yanıt `200`:

```json
{
  "asOfUtc": "2026-11-14T09:00:00Z",
  "currency": "TRY",
  "totalDebt": 38200.00,
  "cardCreditTotal": 0.00,
  "cards": [
    {
      "cardId": "9b7d...",
      "displayName": "Platinum",
      "debt": 4200.00,
      "isInCredit": false
    }
  ],
  "loans": [
    {
      "loanId": "l1o2...",
      "bankName": "Örnek Bank",
      "remainingAmount": 30000.00
    }
  ],
  "personalDebts": [
    {
      "personalDebtId": "pd1e...",
      "counterpartyName": "Ahmet",
      "remainingAmount": 4000.00
    }
  ]
}
```

Hesap:

```
totalDebt = Σ(max(0, kartBorc))
          + Σ(kalanKrediBorcu)
          + Σ(kalanKişiselBorç)
```

- Nakit avans kart borcunun içinde **bir kez** sayılır; ayrıca eklenmez.
- Ekstre tutarı borç toplamına eklenmez.
- Negatif kart bakiyesi (kart alacağı) diğer borçlardan düşülmez;
  `cardCreditTotal` ve ilgili kartta `isInCredit = true` olarak gösterilir.
- Planlanan taksitler borç olarak sayılır; gider sayılmaz.
- Kredi/kişisel borç kaynaklı para girişleri gelir değildir ve borca ikinci kez
  eklenmez.

### 8.3 `GET /reports/upcoming-payments?days=30`

| Parametre | Kural |
|---|---|
| `days` | 1–90; varsayılan 30 |

Yanıt `200` (bilgi amaçlı; yeni gider/borç kaydı oluşturmaz):

```json
{
  "from": "2026-11-14",
  "to": "2026-12-14",
  "currency": "TRY",
  "items": [
    {
      "type": "LoanInstallment",
      "loanId": "l1o2...",
      "installmentNumber": 1,
      "dueDate": "2026-12-01",
      "amount": 3000.00
    },
    {
      "type": "CardStatement",
      "cardId": "9b7d...",
      "statementId": "s9t0...",
      "dueDate": "2026-12-05",
      "amount": 6500.00
    }
  ]
}
```

---

## 9. Durum kodları ve işlem eşlemesi

| İşlem | Başarı | Tipik hatalar |
|---|---|---|
| Liste/tek kayıt | `200` | `401`, `404` |
| Oluşturma | `201` + `Location` | `400`, `409` |
| Güncelleme | `200` | `400`, `404`, `409` |
| İptal/arşiv/logout | `204` | `400`, `404`, `409` |
| Token yenileme | `200` | `401` |

- İş kuralı ihlali → `400 BusinessRuleViolation` veya `409 StateConflict` (kayıt
  mevcut durumunda işlemi kabul etmiyorsa).
- `rowVersion` uyuşmazlığı her zaman `409 ConcurrencyConflict`.
- Başka kullanıcıya ait kayıt her zaman `404 NotFound` (403 kullanılmaz).

---

## 10. Sözleşme kararları

| # | Konu | Karar |
|---|---|---|
| 1 | Para JSON tipi | `number` / `decimal` (`string` değil) |
| 2 | Enum gösterimi | `PascalCase` string |
| 3 | İptal | `POST /{id}/cancel`; `DELETE` yok |
| 4 | Eşzamanlılık | Gövdede `rowVersion`; `ETag/If-Match` yok |
| 5 | Sayfalama | `items/page/pageSize/totalCount/totalPages`; varsayılan 50, üst 200 |
| 6 | İade | `POST /expenses/{id}/refunds`; ayrı gelir kaydı yok |
| 7 | Kart alışverişi | `POST /expenses` + `paymentMethod=CreditCard`; kart uç noktası yok |
| 8 | İdempotency | v1'de opsiyonel `Idempotency-Key`; 24 saat; v1.1'de zorunlu |
| 9 | Auth yanıtı | Register/login/refresh aynı `AuthResponse`; rotating refresh |
| 10 | Profil | `GET/PUT /auth/me`; saat dilimi buradan yönetilir |
| 11 | Kart bakiyesi tarih kesiti | `GET /cards/{id}/balance?asOf=YYYY-MM-DD`; yoksa kullanıcının bugünü; yanıtta `asOfDate`; gelecek `asOf` `400` |
| 12 | Kredi sözleşmesi | `monthlyPayment` + `installmentCount` girdi; `totalRepayment` türetilir; taksitler eşit |

---

## 11. Açık konular

Aşağıdakiler v1 sözleşmesini bloke etmez; `docs/03` §17 ve `docs/05` §11'de izlenir:

- `Idempotency-Key`'in v1.1'de zorunlu hale getirilmesi.
- Parola sıfırlama ve e-posta doğrulama uç noktaları (v1.1).
- Hesap silme / KVKK akışı (v1.1).
- Ekstre belgesi yükleme, OCR ve otomatik içe aktarma (v1.1+).
- Kart harcamaları için taksitli ödeme planı (v1 kapsamı dışı).
- Çoklu para birimi ve kur dönüşümü.
- Kategori geri alma (`restore`) ve kart arşivinden çıkarma: v1'de yok.
- Rate limiting ve kota politikası: v1.1.

> Yeni bir uç nokta, alan veya durum kodu eklemek iş kuralı doğuruyorsa önce
> sorulur; bu belge tek başına yeni iş kuralı yaratmaz.

---

## 12. Değişiklik geçmişi

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-11-14 | İlk sürüm; para/enum/iptal/eşzamanlılık kararları onaylandı |
| 1.1 | 2026-11-14 | Slice 2 uygulama notu: `Idempotency-Key` ayrı göreve ertelendi |
| 1.2 | 2026-11-14 | Slice 3 uygulama notları: kart listesi varsayılanı, bilinmeyen alan reddi, bakiye formülünde eksik terimler |
| 1.3 | 2026-11-15 | Slice 4 bakiye `asOf`/kesit sözleşmesi, ekstre/ödeme listeleri ve türetilen alanlar; kredi `monthlyPayment` girdisi ve türetilen `totalRepayment` |
| 1.4 | 2026-11-15 | Gelecek `asOf` için `400`; iptal zamanının UTC'den kullanıcı yerel tarihine çevrilmesi harcama/iade/ödeme için tek kurala bağlandı |
