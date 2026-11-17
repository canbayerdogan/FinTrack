# FinTrack — Teknik Mimari

**Sürüm:** 1.2 — tasarım
**Bağlı belgeler:** Product Vision & Scope · Finansal Alan Modeli v0.4 · İş Kuralları v1.2
**Hedef okuyucu:** geliştirme ajanları (Codex / Claude) ve geliştirici

---

## 1. Amaç ve kapsam

Bu doküman FinTrack'in .NET çözüm yapısını, modül sınırlarını, katmanlarını ve
teknoloji kararlarını tanımlar.

Kapsam **içinde**: çözüm yapısı, modül sorumlulukları, bağımlılık kuralları, CQRS
yaklaşımı, SQL Server stratejisi, kimlik doğrulama, API tasarımı, MAUI istemcisi,
test stratejisi, MVP dilimleri.

Kapsam **dışında**: iş kurallarının ayrıntısı (doküman 05), API istek/yanıt
sözleşmeleri (doküman 06), ekran tasarımları, OpenAPI şemasının üretimi (kod ile).

---

## 2. Kararların özeti

| # | Konu | Karar | Gerekçe |
|---|---|---|---|
| 1 | Dağıtım | Tek uygulama, tek dağıtım birimi | Ürün ilkesi; mikroservis maliyeti gereksiz |
| 2 | Modül sınırı | 5 iş modülü, derleme zamanında ayrık projeler | Sınır disiplinle değil derleyiciyle korunur |
| 3 | Katman | **Modül içi** Clean Architecture | Belge kararı A; global katman reddedildi |
| 4 | CQRS | Açık Command/Query + Handler, **MediatR yok** | Indirection maliyeti faydayı aşar (ADR-0003) |
| 5 | Veritabanı | Tek SQL Server veritabanı, **modül başına şema + DbContext** | Veri sahipliği; ileride ayrılabilirlik |
| 6 | Modüller arası | Senkron sorgu = `ModuleContracts` arayüzü; olay = in-process | Basit başla, gerektiğinde taşı |
| 7 | Kimlik | ASP.NET Core Identity + JWT (access + rotating refresh) | Standart, mobil uyumlu |
| 8 | İstemci | .NET MAUI, MVVM, Shell | Tek dil, tek ekip |
| 9 | Para | `Money` value object, `decimal(19,4)` | Kayan nokta hatası imkânsız |
| 10 | Silme | Finansal kayıt **hard delete edilmez**, iptal/iade ile düzeltilir | Geçmiş rapor tutarlılığı |

---

## 3. Sistem görünümü

```
┌──────────────────────────────────────────────────────────────┐
│  .NET MAUI (Android / iOS)                                   │
│  MVVM · Shell · typed HttpClient · SecureStorage (token)     │
└───────────────────────────┬──────────────────────────────────┘
                            │ HTTPS / JSON
┌───────────────────────────▼──────────────────────────────────┐
│  FinTrack.Api  (tek dağıtım birimi)                          │
│  ─ composition root, DI, auth, OpenAPI, ProblemDetails        │
│  ┌──────────┬──────────┬──────────┬──────────┬─────────────┐ │
│  │ Expenses │CashInflow│CreditCard│  Debts   │  Reporting  │ │
│  │  modülü  │  modülü  │  modülü  │  modülü  │   modülü    │ │
│  └────┬─────┴────┬─────┴────┬─────┴────┬─────┴──────┬──────┘ │
│       │          │          │          │            │        │
│       └──────────┴──── ModuleContracts (arayüzler) ─┘        │
└───────────────────────────┬──────────────────────────────────┘
                            │ EF Core
┌───────────────────────────▼──────────────────────────────────┐
│  SQL Server — FinTrackDb                                     │
│  şemalar: expenses · inflows · cards · debts · reporting      │
│           identity (Identity tabloları)                       │
│  Modüller arası FK YOK.                                       │
└──────────────────────────────────────────────────────────────┘
```

---

## 4. Çözüm yapısı

```
FinTrack.sln
│
├─ src/
│  ├─ Host/
│  │  └─ FinTrack.Api/                          # ASP.NET Core host
│  │
│  ├─ BuildingBlocks/
│  │  ├─ FinTrack.SharedKernel/                 # Entity, AggregateRoot, Money, Result, hata tipleri
│  │  └─ FinTrack.ModuleContracts/              # modüller arası senkron sorgu arayüzleri
│  │
│  ├─ Modules/
│  │  ├─ Expenses/
│  │  │  ├─ FinTrack.Modules.Expenses.Domain
│  │  │  ├─ FinTrack.Modules.Expenses.Application
│  │  │  ├─ FinTrack.Modules.Expenses.Infrastructure
│  │  │  └─ FinTrack.Modules.Expenses.Endpoints
│  │  ├─ CashInflows/                           # aynı 4 proje
│  │  ├─ CreditCards/                           # aynı 4 proje
│  │  ├─ Debts/                                 # aynı 4 proje
│  │  └─ Reporting/                             # Domain yok (salt okunur) → 3 proje
│  │
│  └─ Mobile/
│     └─ FinTrack.Mobile/                       # .NET MAUI
│
└─ tests/
   ├─ FinTrack.ArchitectureTests/               # sınır ihlallerini yakalayan testler
   ├─ FinTrack.Modules.Expenses.Tests/          # domain + handler
   ├─ FinTrack.Modules.CreditCards.Tests/
   ├─ FinTrack.Modules.Debts.Tests/
   └─ FinTrack.Api.IntegrationTests/            # WebApplicationFactory + Testcontainers
```

### 4.1 Neden modül başına 4 proje?

Sınırları *disiplinle* değil *derleyiciyle* korumak için. `FinTrack.Modules.Expenses.Domain`
projesinin `Microsoft.EntityFrameworkCore` paketine referansı **yoktur**; eklemeye
çalışan geliştirici derleme hatası alır. Bu, "Clean Architecture"ın tek gerçek faydasıdır.

> **Uyarı:** 5 modül × 4 proje = 20 proje. Bu, "kesin sınırlar" isteğinin bedelidir.
> `Endpoints` projelerini `Application` içine katmak mümkündür; ancak o zaman
> Application katmanı ASP.NET Core'a bağımlı olur. Öneri: v1'de ayrı tut, gerekirse
> sonra birleştir.

---

## 5. Modüller ve sorumlulukları

Modül sınırı = **veri sahipliği sınırı**. Bir kavramın sahibi tek modüldür.

| Modül | Sahip olduğu kavramlar | Sahip olmadığı |
|---|---|---|
| **Expenses** | Harcama (kart alışverişi dahil), harcama detayı, iade, kategori | Kart tanımı, borç, ödeme |
| **CashInflows** | Para girişi ve kaynağı | Gelir raporlaması (Reporting) |
| **CreditCards** | Kart tanımı, kart ödemesi, ekstre dönemi, kart borcu, nakit avans | Harcama/kart alışverişi kaydı, kategori, kredi |
| **Debts** | Kredi + taksit planı, kişisel borç (kişiden alınan), geri ödemeler | Kart borcu |
| **Reporting** | Aylık özet projeksiyonları | **Hiçbir finansal kaydın sahibi değildir** |

> **Kart alışverişi Expenses'a aittir** (ADR-0008). CreditCards ayrı bir kart
> işlemi/satın alma kaydı tutmaz; kart borcunu Expenses'taki harcama ve iadeleri
> `ModuleContracts` (`ICardExpenseQuery`) üzerinden okuyarak hesaplar.

### 5.1 Expenses — Harcamalar

**Aggregate:** `Expense`

Sorumluluk: kullanıcının mal/hizmet için yaptığı harcamanın kaydı.

Tutulanlar: tutar, tarih (`OccurredOn`), kategori, açıklama, ödeme yöntemi,
opsiyonel `CardId` (yalnızca kimlik referansı — kartın sahibi CreditCards modülüdür),
kayıt zamanı (`RecordedAt`, UTC).

Kurallar:
- Harcama **her zaman** giderdir. Nakit avans harcama değildir, buraya yazılmaz.
- **Kart alışverişi de bir `Expense`'tir** (`PaymentMethod = CreditCard` + `CardId`).
  Ayrı bir kart işlemi kaydı yoktur; aylık giderde ve kart borcunda aynı kayıttan
  **bir kez** sayılır (ADR-0008).
- Kart borcunun ödenmesi harcama değildir, buraya yazılmaz.
- İade, harcamayı silmez; aynı harcamaya bağlı `Refund` kaydı ile netleştirir
  (doküman 05). Kart iadesi de buradadır; CreditCards borçtan düşer.

### 5.2 CashInflows — Para Girişleri

**Aggregate:** `CashInflow`

Sorumluluk: kullanıcının eline geçen parayı **kaynağıyla** kaydetmek.

`Source` enum: `Salary`, `BusinessIncome`, `BankLoan`, `CashAdvance`, `PersonalBorrowing`,
`AssetSale`, `Unknown`.

Kritik kural: `Source ∈ {BankLoan, CashAdvance, PersonalBorrowing}` ise bu giriş
**kazanılmış gelir değildir** ve ilgili modülde borç yükümlülüğü doğurur.
`IsEarnedIncome` bu kaynaktan **türetilir**, kullanıcı tarafından serbestçe set edilemez.

> v1 kararı: `AssetSale` için kazanç/maliyet hesabı **yapılmaz**. Yalnızca para girişi
> ve kaynağı kaydedilir.

### 5.3 CreditCards — Kredi Kartları

**Aggregates:** `CreditCard` · `CardPayment` · `Statement` · `CashAdvance`

Sorumluluk: kart tanımı, kart ödemeleri, ekstre dönemi ve **hesaplanan** kart borcu.
Kart alışverişi ve iadesi **bu modülün aggregate'i değildir**; Expenses'a aittir
(ADR-0008).

Kart borcu formülü (v1 — ayrıntı doküman 05):

```
kartBorc = Σ(aktif kart harcamaları)          // Expenses'tan okunur
         − Σ(aktif kart iadeleri)             // Expenses'tan okunur
         + Σ(aktif nakit avanslar)
         − Σ(aktif kart ödemeleri)
```

Güncel bakiye formülü **tüm zamanlar** üzerinden çalışır; dönem filtresi
uygulanmaz. `GET /cards/{id}/balance?asOf=...` ve ekstre
`ComputedDebtAtPeriodEnd` için finansal olay tarihine göre kesit uygulanır;
iptal yerel tarihi kesitten sonraysa kayıt kesitte aktif sayılır (ayrıntı
docs/05 §5.3). **Devreden ekstre borcu ayrıca eklenmez** — geçmiş
harcama/ödemelerin doğal sonucudur, eklenirse çift sayım olur.

Kurallar:
- Kart borcu **saklanan bir alan değil, hesaplanan bir durumdur** (ADR-0004 ile tutarlı).
  Performans için `CardBalanceSnapshot` ile önbelleklenebilir; kaynak doğruluk her zaman
  harcama, iade, nakit avans ve ödeme kayıtlarıdır.
- Harcama/iade verisi Expenses modülünden `ICardExpenseQuery` sözleşmesiyle okunur;
  doğrudan tablo erişimi veya modüller arası join yapılamaz.
- **Nakit avans** ayrı aggregate. `CashAdvance` hem `CashInflow` (CashInflows modülünde)
  hem kart borcu bileşeni doğurur. **Toplam borç raporunda bir kez sayılır** — Reporting
  bunu bilmek zorundadır, çift sayım testi vardır.
- Ekstre (`Statement`) dönemsel özettir: `PeriodStart`, `PeriodEnd`, `DueDate`,
  `StatementAmount` kullanıcı tarafından manuel girilir; `CarriedOverAmount`,
  `ComputedDebtAtPeriodEnd` ve `ReconciliationDifference` **okuma anında türetilir,
  saklanmaz**. Aynı kartta yalnızca aktif ekstrelerin dönemleri çakışamaz; iptal
  edilmiş ekstre engel değildir. Ekstre **yeni gider veya ikinci borç kaynağı
  oluşturmaz**; `StatementAmount` borç formülüne girmez.
- Gerçek kart numarası / CVV / SKT alanı **yoktur ve eklenemez**. `BankName`, `DisplayName`,
  `LogoKey` yeterlidir.

### 5.4 Debts — Borçlar

**Aggregates:** `Loan` · `PersonalDebt`

Sorumluluk: taksitli kredi ve kişiden alınan borç.

`Loan`: `Principal` (ele geçen), `MonthlyPayment`, `InstallmentCount`,
`FirstDueDate`. `TotalRepayment = MonthlyPayment × InstallmentCount` türetilir;
taksit tutarları eşittir ve son taksite yuvarlama farkı eklenmez. Taksit planı
`Installment` alt kayıtları olarak üretilir. Faiz/KKDF/BSMV kırılımı **tutulmaz**.

`PersonalDebt`: `CounterpartyName`, `Principal`, `RemainingAmount`, `DueDate?`.

Kurallar:
- Kredi kullanımı `CashInflows` modülünde `BankLoan` kaynaklı bir para girişi doğurur.
  Kredi tutarı **kazanılmış gelir değildir**.
- Planlanan taksit ≠ gerçekleşmiş ödeme. `Installment.Status` bunu ayırır.
- Anapara geri ödemesi tekrar gider sayılmaz; borcu azaltır.

### 5.5 Reporting — Raporlama

**Aggregate yok. Salt okunur.**

Sorumluluk: aylık özet (Z raporu benzeri), borç durumu, kategori kırılımı.

Veri toplar ama **sahibi değildir**. Diğer modüllerin `ModuleContracts` arayüzlerini
kullanır veya kendi okuma modelini (view/projeksiyon) tutar.

Kritik: rapor "gelir − gider" farkını **banka bakiyesi olarak sunmaz**. Yanıt modelinde
`IsBankBalance: false` benzeri açık bir uyarı alanı bulunur.

---

## 6. Modül sınırlarını koruma

### 6.1 Derleme zamanı kuralları

| Kural | Nasıl korunur |
|---|---|
| Modül A, modül B'nin Domain/Application/Infrastructure'ına referans veremez | Proje referansı yok → derleme hatası |
| Modüller arası konuşma yalnızca `FinTrack.ModuleContracts` üzerinden | Tek izinli referans |
| Domain katmanı EF Core / ASP.NET göremez | Paket referansı yok |
| Modül DbContext'i başka modülden erişilemez | `internal` + ayrı assembly |

### 6.2 Mimari testler (zorunlu)

`FinTrack.ArchitectureTests` içinde NetArchTest ile:

```csharp
// Domain hiçbir altyapıya bağımlı olamaz
Types.InAssembly(expensesDomain)
     .ShouldNot().HaveDependencyOnAny(
         "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Net.Http")
     .GetResult().IsSuccessful.Should().BeTrue();

// Modüller birbirinin içine giremez
Types.InAssemblies(allModuleAssemblies)
     .ShouldNot().HaveDependencyOn("FinTrack.Modules.CreditCards.Infrastructure")
     // ... her modül için
     .GetResult().IsSuccessful.Should().BeTrue();
```

Bu testler CI'da zorunlu çalışır. Sınır ihlali = kırmızı build.

### 6.3 Yasak desenler

- ❌ `context.Expenses.Join(context.Cards, ...)` — modüller arası join
- ❌ Modüller arası foreign key
- ❌ Modül B'nin entity'sini modül A'nın handler'ında kullanmak
- ❌ "Kısayol" için Reporting'in doğrudan `cards` şemasına SQL atması

İzinli: `ModuleContracts` arayüzü, in-process domain/integration event, ID referansı.

---

## 7. Katmanlar (modül içi)

```
┌─────────────────────────────────────────────┐
│ Endpoints   minimal API, istek/yanıt eşleme │
├─────────────────────────────────────────────┤
│ Application Command/Query + Handler, DTO,   │
│             validator, port arayüzleri      │
├─────────────────────────────────────────────┤
│ Domain      entity, value object, invariant,│
│             domain event, iş kuralları      │
├─────────────────────────────────────────────┤
│ Infrastructure  EF Core, DbContext,         │
│                 migration, dış servis       │
└─────────────────────────────────────────────┘
```

Bağımlılık yönü **her zaman içeri**: Endpoints → Application → Domain.
Infrastructure, Application'ın port arayüzlerini uygular (Dependency Inversion).

**Kural:** iş kuralı Domain'de yaşar. Application yalnızca akışı yönetir.
"Tutar negatif olamaz" gibi bir kural Handler'da `if` ile yazılmaz; entity'de yaşar.

---

## 8. CQRS yaklaşımı

### 8.1 Karar

CQRS **mantıksal** olarak uygulanır, altyapı olarak değil:

- Yazma: `Command` + `ICommandHandler<TCommand, TResult>`
- Okuma: `Query` + `IQueryHandler<TQuery, TResult>`
- Ayrı modeller: Command → Domain aggregate; Query → doğrudan DTO projeksiyonu
- **Ayrı veritabanı yok.** Tek DB, tek transaction sınırı.
- **MediatR yok.** Handler'lar DI ile doğrudan enjekte edilir.

```csharp
public sealed record CreateExpenseCommand(
    decimal Amount, string Currency, DateOnly OccurredOn,
    Guid CategoryId, string? Description, Guid? CardId)
    : ICommand<Guid>;

internal sealed class CreateExpenseHandler : ICommandHandler<CreateExpenseCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateExpenseCommand cmd, CancellationToken ct)
    {
        var expense = Expense.Create(/* ... */);   // kural Domain'de
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        return expense.Id;
    }
}

// Endpoint
app.MapPost("/api/v1/expenses",
    async (CreateExpenseCommand cmd,
           ICommandHandler<CreateExpenseCommand, Guid> handler,
           CancellationToken ct) =>
    {
        var id = await handler.HandleAsync(cmd, ct);
        return TypedResults.Created($"/api/v1/expenses/{id}", new { id });
    });
```

### 8.2 Neden MediatR yok

- Handler'lar zaten DI'da; mediator yalnızca bir sözlük araması ekler.
- Pipeline behavior'lar (validation, logging) için gereken şey bu ölçekte
  endpoint filter + açık decorator ile çözülür.
- Belge kuralı: "popüler diye teknoloji eklenmez."

### 8.3 MediatR'a geçiş tetikleyicisi

Aşağıdakilerden **ikisi** gerçekleşirse ADR-0003 yeniden açılır:
1. Aynı pipeline davranışı 10+ handler'da tekrar ediyorsa,
2. Modüller arası event dağıtımı 5+ aboneye çıktıysa,
3. Handler sayısı 80'i geçtiyse.

---

## 9. Veri erişimi — SQL Server

### 9.1 Şema yerleşimi

| Şema | Sahip |
|---|---|
| `expenses` | Expenses modülü |
| `inflows` | CashInflows modülü |
| `cards` | CreditCards modülü |
| `debts` | Debts modülü |
| `reporting` | Reporting (yalnızca view / projeksiyon) |
| `identity` | ASP.NET Core Identity |

Her modülün **kendi `DbContext`'i** ve **kendi migration assembly'si** vardır:

```csharp
public sealed class CreditCardsDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("cards");
        b.ApplyConfigurationsFromAssembly(typeof(CreditCardsDbContext).Assembly);
    }
}
```

Migration geçmişi şema başına ayrı: `cards.__EFMigrationsHistory`.

### 9.2 Veri kuralları

- **Para:** `decimal(19,4)`, `Money` value object (`Amount` + `Currency`).
  `float`/`double` **yasak**.
- **Zaman:** tüm damgalar `datetime2` **UTC**. `OccurredOn` ayrı `date` alanıdır
  (yerel takvim günü) — ay sonu raporu ve "hangi gün girdi" analizi bunu gerektirir.
- **Eşzamanlılık:** aggregate'lerde `rowversion`.
- **Silme:** finansal kayıtlarda hard delete yok. `Status` (`Active`, `Cancelled`, `Refunded`)
  + `CancelledAt`, `CancellationReason`.
- **Çok kiracılılık:** her aggregate `UserId` taşır. Global query filter zorunlu.
- **Modüller arası FK yok.** `CardId` yalnızca `uniqueidentifier` değerdir; bütünlük
  Application katmanında `ModuleContracts` ile doğrulanır.

### 9.3 Migration uygulama

Tek host, N DbContext → sıralı uygulama. `FinTrack.Api` başlangıcında:

```csharp
await app.MigrateModulesAsync();  // her modülün migration'ını sırayla uygular
```

Üretimde migration **otomatik çalışmaz**; `--migrate` bayrağı veya CI adımı ile çalışır.

---

## 10. Kimlik doğrulama ve yetkilendirme

- **ASP.NET Core Identity** (kullanıcı, parola hash'i) + **JWT**
- Access token: 15 dk · Refresh token: 30 gün, **rotating** ve DB'de hash'li
- Mobil: token'lar `SecureStorage`'da (Android Keystore / iOS Keychain)
- Her istekte `UserId` claim → `ICurrentUser`
- **Global query filter:** kullanıcı yalnızca kendi verisini görür

```csharp
b.Entity<Expense>().HasQueryFilter(e => e.UserId == _currentUser.Id);
```

- Mobil **güvenilmeyen istemci** kabul edilir. Tüm doğrulama sunucuda tekrar yapılır.
- Parola politikası, e-posta doğrulama, hesap silme (veri silme hakkı) → doküman 05.

---

## 11. Para, tarih ve zaman dilimi

| Konu | Kural |
|---|---|
| Para birimi | v1: yalnızca `TRY`. Model çoklu birime açık (`Money.Currency`) |
| Yuvarlama | `MidpointRounding.AwayFromZero`, 2 hane gösterim / 4 hane saklama |
| Damga | UTC sakla, kullanıcı saat diliminde göster |
| Rapor dönemi | Kullanıcının saat diliminde ay sınırları (varsayılan `Europe/Istanbul`) |
| "Gün" | `DateOnly` — saat dilimi kayması olmaz |

> **Karar:** kullanıcı başına saat dilimi `User.PreferredTimeZone` alanında tutulur,
> varsayılan `Europe/Istanbul`; IANA kimliği doğrulanır (doküman 05 §2.6).

---

## 12. API tasarımı

### 12.1 Genel

- Taban: `/api/v1`
- Kaynak adları İngilizce (`/expenses`), UI metinleri Türkçe
- Hata: **ProblemDetails** (RFC 9457)
- Dokümantasyon: OpenAPI + Scalar UI → tarayıcıdan ve Postman'den test
- Sayfalama: `?page=1&pageSize=50` (v1), cursor v2
- Tarih formatı: `yyyy-MM-dd` · damga: ISO 8601 UTC

### 12.2 Uç noktalar (v1 özeti)

> Ayrıntılı istek/yanıt sözleşmeleri: [API Sözleşmeleri v1.0](06-api-sozlesmeleri.md).
> Finansal kayıtlarda `DELETE` yoktur; iptal `POST /{id}/cancel`, kart/kategori
> için `POST /{id}/archive` kullanılır.

| Modül | Uç nokta |
|---|---|
| Auth | `POST /auth/register` · `POST /auth/login` · `POST /auth/refresh` · `POST /auth/logout` · `GET/PUT /auth/me` |
| Expenses | `GET/POST /expenses` · `GET/PUT /expenses/{id}` · `POST /expenses/{id}/cancel` · `GET/POST /expenses/{id}/refunds` · `POST /expenses/{id}/refunds/{refundId}/cancel` · `GET/POST /categories` · `PUT /categories/{id}` · `POST /categories/{id}/archive` |
| CashInflows | `GET/POST /cash-inflows` · `GET/PUT /cash-inflows/{id}` · `POST /cash-inflows/{id}/cancel` |
| CreditCards | `GET/POST /cards` · `GET/PUT /cards/{id}` · `POST /cards/{id}/archive` |
| | `GET /cards/{id}/balance` · `GET/POST /cards/{id}/statements` · `POST /cards/{id}/statements/{statementId}/cancel` |
| | `GET/POST /cards/{id}/payments` · `POST /cards/{id}/payments/{paymentId}/cancel` |
| | `GET/POST /cards/{id}/cash-advances` · `POST /cards/{id}/cash-advances/{cashAdvanceId}/cancel` |
| Debts | `GET/POST /loans` · `GET /loans/{id}` · `POST /loans/{id}/cancel` · `POST /loans/{id}/installments/{no}/pay` · `POST /loans/{id}/installments/{no}/payment/cancel` |
| | `GET/POST /personal-debts` · `GET /personal-debts/{id}` · `POST /personal-debts/{id}/cancel` · `GET/POST /personal-debts/{id}/repayments` · `POST /personal-debts/{id}/repayments/{repaymentId}/cancel` |
| Reporting | `GET /reports/monthly?year=2026&month=11` · `GET /reports/debt-summary` · `GET /reports/upcoming-payments?days=30` |

### 12.3 İdempotency

Mobil ağ kopmalarında çift kayıt oluşmaması için `POST` uç noktaları
`Idempotency-Key` başlığını destekler. v1.1'de zorunlu hale getirilmesi önerilir;
ileride ekstre içe aktarma mükerrer tespiti bunun üzerine kurulacaktır.

---

## 13. Hata yönetimi ve gözlemlenebilirlik

| Konu | Karar |
|---|---|
| Doğrulama | `FluentValidation` (Application katmanında, handler öncesi) |
| İş kuralı hatası | `Result` / `DomainException` → 400 veya 409 |
| Beklenmeyen hata | 500 + `traceId`, kullanıcıya iç detay gösterilmez |
| Log | `Serilog` → konsol (dev) / dosya+Seq (prod) |
| Sağlık | `GET /health` (liveness) · `GET /health/ready` (DB dahil) |
| Telemetri | OpenTelemetry (v1.1) |

---

## 14. Mobil istemci — .NET MAUI

| Konu | Karar |
|---|---|
| Desen | MVVM — `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) |
| Navigasyon | Shell + route tabanlı |
| HTTP | `IHttpClientFactory` + modül başına typed client |
| Token | `SecureStorage`, 401'de otomatik refresh + tekrar deneme |
| Yerel önbellek | v1: yalnızca okuma önbelleği (SQLite). **Çevrimdışı yazma yok** |
| Bağlantı | Emülatör: `http://10.0.2.2:5xxx` · cihaz: LAN IP · prod: HTTPS |
| DTO paylaşımı | v1'de istemci tarafında `FinTrack.Mobile.Core.Api` DTO'ları; `FinTrack.Contracts` ayrı projesi v1.1'e ertelendi |

**Ekranlar (v1):** Giriş/Kayıt · Ana özet · Harcama listesi/ekleme · Kartlar · Kart detay
(işlemler, borç, ekstre) · Borçlar · Raporlar · Ayarlar.

> MAUI iş yükleri kuruldu: `dotnet workload install maui-android maui-windows`.
> Hedefler Android + Windows (Slice 9); okuma önbelleği v1'de eklenmedi.
> Kod: `src/Mobile/FinTrack.Mobile.Core` + `src/Mobile/FinTrack.Mobile`.

---

## 15. Test stratejisi

| Katman | Araç | Ne test edilir |
|---|---|---|
| Domain | xUnit | İnvariantlar, borç formülleri, çift sayım kuralları |
| Application | xUnit + SQLite/Testcontainers | Handler akışı, doğrulama |
| API | `WebApplicationFactory` + Testcontainers MSSQL | Uçtan uca, auth, yetki |
| Mimari | NetArchTest | Katman ve modül sınırları |
| Mobil | xUnit (VM) | ViewModel mantığı |

**Zorunlu regresyon testleri** (ayrıntı: doküman 05 §9):
1. Kart borcu ödemesi ikinci kez gider oluşturmaz.
2. Nakit avans toplam borçta bir kez sayılır.
3. Kredi tutarı kazanılmış gelir sayılmaz.
4. Planlanan taksit, ödenmiş sayılmaz.
5. Kullanıcı başka kullanıcının kaydını okuyamaz/yazamaz.
6. Kart alışverişi aylık giderde bir kez, kart borcunda bir kez görünür; iki ayrı
   gider kaydı oluşmaz.
7. Kart iadesi hem net harcamayı hem kart borcunu azaltır; gelir oluşturmaz.
8. Ekstre tutarı kart borcunu veya gideri değiştirmez.
9. Devreden ekstre borcu kart borcuna ikinci kez eklenmez.
10. Kredi/kişisel borç oluşturma, bağlı para girişi olmadan tamamlanmaz.

---

## 16. MVP dilimleri

Her dilim uçtan uca çalışır (API + test). Dikey dilim mantığı. Ayrıntılı kapsam,
kabul kriterleri ve ajan görev standardı: [Yol Haritası v1.1](07-yol-haritasi-ve-ajan-gorev-standardi.md).

| # | Dilim | Kapsam |
|---|---|---|
| 0 | İskelet | Solution, SharedKernel, Api host, health, OpenAPI, mimari testler |
| 1 | Kimlik | Register/login/refresh, JWT, kullanıcı izolasyonu |
| 2 | Harcama | Kategori + harcama CRUD, filtre, sayfalama |
| 3 | Kart | Kart CRUD, hesaplanan bakiye; kart alışverişi Expenses üzerinden (`CardId`) |
| 4 | Ekstre + ödeme | Ekstre dönemi, kart ödemesi, borç formülü |
| 5 | Para girişi | Kaynak türleri, `IsEarnedIncome` türetimi |
| 6 | Nakit avans | CashAdvance aggregate, kart borcu entegrasyonu, çift sayım testi |
| 7 | Kredi + kişisel borç | Taksit planı üretimi, taksit ödeme, kalan borç |
| 8 | Raporlama | Aylık özet, borç durumu, kategori kırılımı |
| 9 | MAUI | Giriş, özet, harcama, kart, borç ekranları |

---

## 17. Açık ve kapanan kararlar

| # | Konu | Karar | Durum |
|---|---|---|---|
| 1 | Kullanıcı saat dilimi alanı | `User.PreferredTimeZone`, varsayılan `Europe/Istanbul` (doküman 05 §2.6) | ✅ Kapandı |
| 2 | İdempotency zorunlu mu | v1'de desteklenir, v1.1'de zorunlu | ⏳ |
| 3 | Ekstre: manuel mi hesaplanan mı | Tarihler + tutar manuel; devreden borç ve hesaplanan borç türetilir (doküman 05 §5.6) | ✅ Kapandı |
| 4 | Nakit avans mutabakat kuralları | Kart borcuna eklenir; geri ödeme kart ödemesidir; tek sayım (doküman 05 §5.5) | ✅ Kapandı |
| 5 | Çoklu para birimi | v1 tek (TRY) | ⏳ |
| 6 | `Endpoints` projeleri ayrı mı | v1 ayrı | ⏳ |
| 7 | Parola sıfırlama / e-posta doğrulama | v1.1 | ⏳ |
| 8 | Hesap silme (KVKK) | v1.1, soft delete + 30 gün | ⏳ |

Kalan maddeler v1 iş kurallarını bloke etmez. Bir ajan bunları kendi varsayımıyla
kesinleştiremez; önce sorar. İş kurallarına dair açık nokta kalmamıştır
([doküman 05](05-is-kurallari.md) §11).
