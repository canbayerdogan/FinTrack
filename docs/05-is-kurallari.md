# FinTrack — İş Kuralları (Business Rules)

**Sürüm:** 1.2
**Tarih:** 2026-11-15
**Bağlı belgeler:** Finansal Alan Modeli v0.4 · Teknik Mimari v1.2 · ADR-0001…0009
**Hedef okuyucu:** geliştirme ajanları ve geliştirici

> Bu doküman v1 iş kurallarını tanımlar. `docs/02` §5'teki 7 açık karar ile
> kart alışverişi sahipliği burada kapatılmıştır. Kuralda boşluk varsa ajan
> kendi varsayımıyla kesinleştiremez; önce sorar.

---

## 1. Amaç, kapsam ve ilkeler

Bu doküman; harcama, para girişi, kredi kartı ve borç kayıtlarının hangi koşulda
oluştuğunu, nasıl değiştiğini, hangi durumda geçersiz sayıldığını ve raporlara
nasıl yansıdığını tanımlar.

### 1.1 Değişmez ilkeler

1. **Hesaplama otoritesi uygulamadır.** Bakiye, borç ve toplamlar deterministik
   kodla hesaplanır. AI yalnızca açıklama/öneri üretebilir; hesaplama sonucunu
   değiştiremez.
2. **Harcama, ödeme, para girişi ve borç ayrı kavramlardır.** Tek bir genel
   "işlem" tablosuna indirgenemez.
3. **Aynı ekonomik olay iki kez gelir/gider/borç sayılmaz.**
4. **Finansal kayıt hard delete edilmez.** İptal, iade veya düzeltme ile iz
   bırakılır.
5. **Kaynak türü korunur.** Kredi, nakit avans ve kişiden borç kazanılmış gelir
   değildir.
6. **Belirsiz bilgi tahmin edilmez.** Kaynağı belirsiz para girişi "belirsiz"
   kalır; AI veya uygulama otomatik sınıflandırmaz.
7. **Mobil istemci güvenilmezdir.** Tüm doğrulama sunucuda tekrar yapılır.
8. **Gerçek kart numarası, CVV, son kullanma tarihi veya banka giriş bilgisi
   istenmez ve saklanmaz.**

### 1.2 Kapsam dışı

- Banka API entegrasyonu, ekstre belgesi yükleme/OCR (v1.1+).
- Muhasebe kırılımı (anapara/faiz/KKDF/BSMV ayrıştırması).
- Yatırım/varlık portföyü ve varlık satış kazancı hesabı.
- Abonelik yaşam döngüsü (v2).
- Çoklu para birimi ve kur dönüşümü (v1 yalnızca TRY).

---

## 2. Ortak kurallar

### 2.1 Para

| Kural | Değer |
|---|---|
| Para birimi | v1 yalnızca `TRY`; model `Money.Currency` ile çoklu birime açık |
| Saklama | `decimal(19,4)`; `float`/`double` yasak |
| Gösterim | 2 hane, `MidpointRounding.AwayFromZero` |
| Yuvarlama | Ara toplamlarda yuvarlama yapılmaz; yalnızca gösterimde 2 hane |
| Kur dönüşümü | v1'de yok; farklı para birimi hata verir |

### 2.2 Tarih ve saat

| Alan | Tip | Kural |
|---|---|---|
| `OccurredOn`, `PaidOn`, `RefundedOn`, `DueDate` | `DateOnly` | Kullanıcının yerel takvim günü; saat dilimi kayması olmaz |
| `RecordedAtUtc`, `CancelledAtUtc` | `datetime2` UTC | Sunucuda `IClock.UtcNow` ile üretilir |
| Rapor dönemi | Takvim ayı | Kullanıcının saat diliminde; varsayılan `Europe/Istanbul` |
| Gelecek tarih | — | Gerçekleşmiş kayıtlar (`Expense`, `CashInflow`, `CardPayment`, `Refund`, `CashAdvance`, `PersonalDebtRepayment`) bugünden ileri olamaz. Planlanan taksit ve ekstre son ödeme tarihi ileri olabilir. |

### 2.3 Silme ve iptal

- Finansal kayıtlar **hard delete edilmez**.
- İptal edilen kayıt `Status = Cancelled`, `CancelledAtUtc` ve zorunlu
  `CancellationReason` taşır.
- İptal edilen kayıtlar tüm rapor toplamlarından **hariç tutulur**; geçmiş
  listelerde durumuyla görünür.
- İptal, bağlı kayıtları da tutarlı biçimde etkiler (bkz. §9).

### 2.4 Kullanıcı izolasyonu

- Her aggregate `UserId` taşır; EF Core global query filter zorunludur.
- Kullanıcı başka kullanıcının kaydını okuyamaz, güncelleyemez, iptal edemez.
- Başka kullanıcıya ait kayda erişim **404** döner (kaydın varlığını sızdırmamak
  için 403 değil).

### 2.5 Eşzamanlılık

- Aggregate'lerde `rowversion` kullanılır.
- Güncelleme sırasında satır sürümü değişmişse istek **409 Conflict** ile
  reddedilir; kullanıcıya yeniden yükleme önerilir.

### 2.6 Kimlik ve oturum

- Kayıt: e-posta + parola. E-posta normalize edilir ve kullanıcı başına benzersizdir.
- Parola: en az 8 karakter, en az bir harf ve bir rakam. En fazla 128 karakter.
- Access token 15 dakika; refresh token 30 gün, **rotating** ve DB'de hash'li.
- Kullanıcı saat dilimi `PreferredTimeZone` alanında tutulur; IANA kimliği
  doğrulanır; varsayılan `Europe/Istanbul`.
- E-posta doğrulama, parola sıfırlama ve hesap silme v1.1 kapsamındadır.
- Tüm oturum ve yetki kontrolleri sunucuda yapılır; istemciden gelen `UserId`
  alanına güvenilmez.

### 2.7 İdempotency

- v1'de `Idempotency-Key` başlığı **desteklenir** ancak zorunlu değildir.
- v1.1'de tüm `POST` uç noktalarında zorunlu hale getirilmesi planlanır.
- Aynı anahtarla gelen tekrar istek yeni kayıt oluşturmaz; ilk yanıt döner.

---

## 3. Harcamalar (Expenses)

### 3.1 Kayıt kuralları

| Kural | Açıklama |
|---|---|
| `Amount` | `> 0` ve `TRY` |
| `OccurredOn` | Bugün veya geçmiş; gelecek tarihli gerçekleşmiş harcama yok |
| `CategoryId` | Zorunlu; kullanıcıya ait ve arşivlenmemiş kategori |
| `Description` | Opsiyonel; en fazla 500 karakter |
| `PaymentMethod` | `Cash`, `DebitCard`, `CreditCard`, `BankTransfer`, `Other` |
| `CardId` | Yalnızca `PaymentMethod = CreditCard` iken zorunlu; diğerlerinde `null` |
| `RecordedAtUtc` | Oluşturulma anı; sonradan değiştirilemez |

- `CardId` yalnızca bir **kimlik referansıdır**. Kartın sahibi CreditCards
  modülüdür; Expenses kart numarası/CVV gibi veri tutmaz.
- Kart, kullanıcıya ait ve `Active` olmalıdır. Arşivlenmiş karta yeni harcama
  yazılamaz.

### 3.2 Kart alışverişi sahipliği (ADR-0008)

- **Kredi kartıyla yapılan alışveriş bir `Expense` kaydıdır.** Ayrı bir
  "kart alışverişi" kaydı oluşturulmaz.
- Kart alışverişi, kart borcunun **tek doğruluk kaynağıdır**; CreditCards bu
  harcamaları `ModuleContracts` üzerinden okur.
- Kart harcaması aylık gider raporunda **bir kez** görünür; kart borcunda da
  aynı kayıttan hesaplanır. İki ayrı gider/borç kaydı yoktur.
- Kart iadesi de aynı harcamaya bağlı `Refund` kaydıdır; CreditCards iadeyi
  kart borcundan düşer.

### 3.3 Güncelleme

- Tutar, tarih, kategori, açıklama, ödeme yöntemi ve kart alanları
  güncellenebilir.
- `RecordedAtUtc` değişmez.
- İptal edilmiş harcama güncellenemez.
- Kısmen veya tamamen iade edilmiş harcamanın tutarı, **aktif iadeler toplamının
  altına** düşürülemez.
- Ödeme yöntemi `CreditCard` dışına alınırsa `CardId` temizlenir; `CreditCard`
  yapılırsa geçerli `CardId` zorunludur.

### 3.4 İptal

- Harcama silinmez; `Cancelled` yapılır. `CancellationReason` zorunludur.
- İptal edilmiş harcamaya yeni iade eklenemez.
- Mevcut iadeler iptal edilmeden harcama iptal edilemez; önce iadeler iptal
  edilir. (Aksi halde iade, iptal edilmiş bir harcamayı azaltıyor görünür.)

### 3.5 İade (Refund)

- İade ayrı bir `Refund` kaydıdır; harcamayı silmez, **net tutarı azaltır**.
- `Amount > 0`, `RefundedOn >= Expense.OccurredOn` ve `RefundedOn <= bugün`.
- Birden fazla iade olabilir; **aktif iadeler toplamı** harcama tutarını aşamaz.
- İade iptal edilebilir; iptal edilen iade net tutara tekrar eklenir.
- Harcama durumu iadelerden **türetilir**:
  - iade yok → `Active`
  - `0 < iadeler < tutar` → `PartiallyRefunded`
  - `iadeler = tutar` → `Refunded`
  - iptal → `Cancelled`
- İade, yeni kazanç/gelir olarak raporlanmaz; ilgili harcamanın kategorisinde
  gider azaltıcı etki yapar.

### 3.6 Kategoriler

- Kategoriler kullanıcıya aittir; v1'de başlangıç (sistem) kategorileri
  tanımlanabilir.
- Aynı kullanıcıda kategori adı büyük/küçük harf duyarsız benzersizdir.
- Harcaması olan kategori silinemez; **arşivlenir**.
- Arşivlenmiş kategori mevcut harcamalarda görünmeye devam eder; yeni harcamada
  seçilemez.
- Kart alışverişleri de bu kategorileri kullanır (çünkü onlar da `Expense`'tir).

### 3.7 Harcama olmayanlar

Aşağıdakiler **Expenses modülüne yazılmaz**:

- Kredi kartı borcu ödemesi (CreditCards'ta `CardPayment`).
- Nakit avans kullanımı (CreditCards'ta `CashAdvance`).
- Kredi taksiti veya kişisel borç geri ödemesi (Debts'te).
- Kredi/nakit avans/kişisel borç kaynaklı para girişi (CashInflows'ta).

---

## 4. Para girişleri (CashInflows)

### 4.1 Kayıt kuralları

| Kural | Açıklama |
|---|---|
| `Amount` | `> 0` ve `TRY` |
| `OccurredOn` | Bugün veya geçmiş |
| `Source` | Zorunlu; aşağıdaki 7 değerden biri |
| `Description` | Opsiyonel; en fazla 500 karakter |
| `IsEarnedIncome` | Kullanıcı tarafından **set edilemez**; `Source`'tan türetilir |

### 4.2 Kaynak türleri ve gelir niteliği

| `Source` | `IsEarnedIncome` | Borç etkisi | Aylık özet grubu |
|---|---|---|---|
| `Salary` | `true` | Yok | Kazanılmış gelir |
| `BusinessIncome` | `true` | Yok | Kazanılmış gelir |
| `BankLoan` | `false` | Debts'te `Loan` | Diğer para girişleri |
| `CashAdvance` | `false` | CreditCards'ta `CashAdvance` | Diğer para girişleri |
| `PersonalBorrowing` | `false` | Debts'te `PersonalDebt` | Diğer para girişleri |
| `AssetSale` | `false` | Yok | Diğer para girişleri |
| `Unknown` | `null` (belirsiz) | Yok | **Belirsiz** (ayrı grup) |

- `Unknown`, kazanılmış gelir toplamına **dahil edilmez**; "diğer para
  girişleri"ne de karıştırılmaz. Ayrı "Belirsiz" grubunda gösterilir ve
  kullanıcıya "kaynağı belirt" uyarısı verilir.
- Uygulama veya AI, `Unknown` kaynağı otomatik tahmin edemez.
- `AssetSale` için alış maliyeti, kazanç veya zarar hesaplanmaz; yalnızca para
  girişi ve kaynağı kaydedilir.

### 4.3 Borç doğuran para girişleri

- `BankLoan`, `CashAdvance` ve `PersonalBorrowing` kaynaklı para girişleri
  **tek başına oluşturulamaz**.
- İlgili borç kaydı ile **aynı kullanıcı işleminde** oluşturulur:
  - `BankLoan` → Debts'te `Loan`
  - `CashAdvance` → CreditCards'ta `CashAdvance`
  - `PersonalBorrowing` → Debts'te `PersonalDebt`
- Biri başarısız olursa diğeri de kalıcı olmaz; yarım kayıt bırakılmaz.
- Bu kaynaklardaki para girişi **kazanılmış gelir değildir**.
- Borç kaydı iptal edilirse bağlı para girişi de iptal edilir. Borç kaydına bağlı
  para girişi doğrudan iptal edilemez.

### 4.4 İptal

- `Salary`, `BusinessIncome`, `AssetSale` ve `Unknown` kaynaklı girişler iptal
  edilebilir; `CancellationReason` zorunludur.
- İptal edilen giriş rapor toplamlarından çıkarılır.

### 4.5 Ödeme yönetimi yok

- CashInflows modülü **yalnızca para girişini** yönetir.
- Kart ödemesi, nakit avans geri ödemesi, kredi taksiti ve kişisel borç geri
  ödemesi bu modülde tutulmaz; yükümlülüğün sahibi modülde yaşar (ADR-0007).

---

## 5. Kredi kartları (CreditCards)

### 5.1 Kart tanımı

| Alan | Kural |
|---|---|
| `BankName` | Zorunlu |
| `DisplayName` | Zorunlu; kullanıcıya görünen ad |
| `LogoKey` | Opsiyonel; görsel anahtarı |
| `CreditLimit` | Opsiyonel; bilgi amaçlı, borç hesabına girmez |
| `StatementDay` | Opsiyonel; 1–31 |
| `PaymentDueDay` | Opsiyonel; 1–31 |
| `Status` | `Active`, `Archived` |

- Gerçek kart numarası, CVV, son kullanma tarihi, banka şifresi **yoktur ve
  eklenemez**.
- Arşivlenmiş karta yeni harcama, ödeme, nakit avans veya ekstre eklenemez;
  mevcut kayıtlar korunur.
- Kart silinmez; arşivlenir.

### 5.2 Kart alışverişi

- Kart alışverişi **Expenses modülünde** `Expense` + `CardId` olarak tutulur
  (ADR-0008).
- CreditCards modülü ayrı bir satın alma işlemi kaydı tutmaz.
- Kart borcu hesabı için alışveriş ve iadeler `ICardExpenseQuery` sözleşmesiyle
  okunur; doğrudan tablo erişimi yapılamaz.

### 5.3 Kart borcu formülü

```
kartBorc = Σ(aktif kart harcamaları)
         − Σ(aktif kart iadeleri)
         + Σ(aktif nakit avanslar)
         − Σ(aktif kart ödemeleri)
```

- **Güncel kart borcu** formülü tüm zamanlar üzerinden çalışır; dönem filtresi
  uygulanmaz.
- **Kesitli hesap** (`GET /cards/{id}/balance?asOf=...` ve ekstre
  `ComputedDebtAtPeriodEnd`) için kesit tarihi, kullanıcının saat diliminde bir
  `DateOnly`'dir. Finansal olaylarda `OccurredOn` / `RefundedOn` / `PaidOn` /
  `TakenOn` tarihi kesit gününe **dahil** edilir; sonraki gün hareketleri hesaba
  girmez.
- `asOf` verilmezse kullanıcının bugünkü tarihi kullanılır. Gelecek tarihli
  `asOf` geçersizdir: kullanıcının bugününden sonraki tarih
  `400 ValidationFailed` döner; bugüne kırpılmaz. Varsayılan bugün olduğundan ve
  gelecek tarihli finansal kayıt oluşturulamadığından, parametresiz hesap mevcut
  durumun tüm zamanlar toplamıyla aynı sonucu verir.
- **Kesitte aktiflik:** Bir kayıt `CancelledAtUtc` taşımıyorsa aktif sayılır.
  `CancelledAtUtc` UTC zamanı kullanıcının saat dilimine çevrilerek yerel iptal
  tarihi bulunur:
  - yerel iptal tarihi kesit tarihinden **önce veya eşit** ise kayıt kesitte
    **iptal** sayılır ve toplama girmez;
  - yerel iptal tarihi kesit tarihinden **sonra** ise kayıt kesitte **aktif**
    sayılır.
  - Aynı kural **harcama, iade ve kart ödemesi** için geçerlidir.
- Ekstre `ComputedDebtAtPeriodEnd` hesabında kesit tarihi ilgili ekstre
  `PeriodEnd`'idir; sunucu saatine göre "şimdi" kullanılmaz.
- **Devreden ekstre borcu ayrıca eklenmez.** Devreden borç, geçmiş harcama ve
  ödemelerin doğal sonucudur; ayrıca eklenirse çift sayım olur.
- Kart borcu **saklanan bir alan değil, hesaplanan bir durumdur**. Performans için
  snapshot tutulabilir; kaynak doğruluk her zaman kayıtlardır.
- Sonuç negatifse bu bir hata değildir; "kart lehine bakiye (alacak)" olarak
  gösterilir. Toplam borç raporunda negatif kart bakiyesi diğer borçlardan
  düşülmez; ayrı bilgi olarak gösterilir.

### 5.4 Kart ödemesi

- `CardPayment`: `Amount > 0`, `PaidOn <= bugün`, kart kullanıcıya ait ve
  arşivlenmemiş olmalı.
- Kart ödemesi **gider değildir**; daha önce kaydedilmiş alışverişi ikinci kez
  gider saymaz.
- Ödeme mevcut borcu aşabilir; bakiye negatife düşer ve kart alacağı olarak
  gösterilir. Bu durum hata değildir.
- `PaidOn` finansal gerçekleşme tarihidir; iptal zamanı ayrı bir sistem
  zamanıdır (`CancelledAtUtc`, sunucuda UTC). İptal `PaidOn` değerini değiştirmez.
- Ödeme iptal edilebilir; `CancellationReason` zorunludur. İptal edilen ödeme
  güncel bakiyeye girmez; borcu geri yükseltir. Kesitli hesapta `CancelledAtUtc`
  UTC zamanı kullanıcı saat dilimine çevrilir; yerel iptal tarihi kesit
  tarihinden sonra ise ödeme kesitte aktif sayılır, eşit veya önce ise girmez
  (bkz. §5.3).
- Ödeme-ekstre eşleştirmesi v1'de yoktur; ödeme yalnızca `PaidOn` tarihiyle
  kesite girer.
- İptal, önceki bir ekstre kesitinde ödenmiş görünen tutarı geriye dönük
  değiştirebilir; ekstrenin türetilen alanları okuma anında yeniden hesaplanır.
- Ödeme hard delete edilmez.

### 5.5 Nakit avans

- `CashAdvance`: `Amount > 0`, `TakenOn <= bugün`, kart kullanıcıya ait ve
  arşivlenmemiş olmalı.
- Nakit avans **kart borcunun bileşenidir** ve aynı zamanda `CashInflow`
  (`Source = CashAdvance`) doğurur.
- Nakit avans **gider değildir**; alındığı anda tüketim gideri oluşturmaz.
- Nakit avans geri ödemesi için ayrı bir kayıt türü yoktur; **kart ödemesi**
  olarak girilir ve toplam kart borcunu azaltır.
- Nakit avans toplam borçta **bir kez** sayılır: kart borcunun içinde. Ayrıca
  Debts benzeri ikinci bir borç kalemi olarak eklenmez.
- Nakit avans iptal edilirse bağlı para girişi de iptal edilir; kart ödemeleri
  geri alınmaz, borç yeniden hesaplanır.

### 5.6 Ekstre

Kullanıcı aşağıdaki alanları **manuel** girer:

| Alan | Kural |
|---|---|
| `PeriodStart` | Zorunlu; `PeriodStart <= PeriodEnd` |
| `PeriodEnd` | Zorunlu |
| `DueDate` | Zorunlu; `DueDate >= PeriodEnd` |
| `StatementAmount` | Zorunlu; `>= 0` |

- Aynı kartta **aktif** ekstrelerin dönemleri çakışamaz. Çakışma, iki dönemin
  gün kümelerinin kesişmesidir (`PeriodStart <= diğer.PeriodEnd` ve
  `PeriodEnd >= diğer.PeriodStart`); başlangıç ve bitiş günleri dahildir.
  İptal edilmiş ekstre yeni ekstre oluşturulmasını engellemez.
- Ekstre **yeni gider veya ikinci borç kaynağı değildir**. `StatementAmount`
  kart borcuna eklenmez.
- **Devreden borç** kullanıcı tarafından girilmez; okuma anında türetilir:
  - Aynı kartta, mevcut `PeriodEnd`'den küçük `PeriodEnd`'e sahip **aktif**
    ekstreler arasından en büyük `PeriodEnd`'li olan "önceki aktif ekstre"dir.
    Yoksa `CarriedOverAmount = 0`.
  - Varsa:
    `max(0, önceki.StatementAmount − Σ(PaidOn ∈ (önceki.PeriodEnd, mevcut.PeriodEnd] olan ve mevcut.PeriodEnd kesitinde aktif kart ödemeleri))`
  - Aralık alt sınırı **hariç**, üst sınır **dahildir**. Yalnızca `PaidOn`
    finansal tarihi esas alınır; ödeme-ekstre eşleştirmesi yapılmaz.
  - Bu değer yalnızca bilgi amaçlıdır; borç formülüne girmez.
- `ComputedDebtAtPeriodEnd`, ilgili `PeriodEnd` gününün sonu (kullanıcı saat
  diliminde) kesitiyle hesaplanan kart borcudur. Formül §5.3'teki gibidir;
  harcama, iade, nakit avans ve ödemelerde finansal olay tarihi kesite dahil
  edilir; iptal aktifliği §5.3'teki UTC→yerel tarih kuralına göre belirlenir.
- `ReconciliationDifference = StatementAmount − ComputedDebtAtPeriodEnd`.
  Fark otomatik olarak gider, faiz veya borç kaydı **oluşturmaz**; kullanıcıya
  gösterilir. Kullanıcı isterse farkı ayrı bir harcama olarak girebilir.
- `CarriedOverAmount`, `ComputedDebtAtPeriodEnd` ve `ReconciliationDifference`
  **saklanmaz; okuma anında güncel kayıtlardan hesaplanır.** Böylece sonradan
  girilen geçmiş tarihli iade/ödeme veya iptal, geçmiş ekstrenin türetilen
  alanlarını günceller.
- Ekstre iptal edilebilir; iptal borç hesabını değiştirmez, dönem çakışması
  engeli oluşturmaz ve hard delete yapılmaz.

### 5.7 Kart işlem türleri

CreditCards modülünde tutulan kayıtlar:

- `CreditCard` — kart tanımı
- `CardPayment` — kart borcu ödemesi
- `CashAdvance` — kartla ilişkili nakit avans
- `Statement` — dönemsel ekstre özeti

Kart **alışverişi ve iadesi** bu listede yoktur; Expenses'a aittir (ADR-0008).

---

## 6. Borçlar (Debts)

### 6.1 Kredi (Loan)

| Alan | Kural |
|---|---|
| `Principal` | `> 0`; kullanıcının eline geçen tutar |
| `MonthlyPayment` | `> 0`; aylık taksit tutarı; en fazla 4 ondalık |
| `InstallmentCount` | `>= 1`; v1 üst sınırı 120 |
| `FirstDueDate` | Kredi tarihinden önce olamaz |
| `BankName` | Opsiyonel |
| `Description` | Opsiyonel |

- Kredi oluşturulduğunda **aynı işlemde** `CashInflow` (`Source = BankLoan`,
  tutar = `Principal`) oluşturulur.
- Kredi tutarı **kazanılmış gelir değildir**; aylık özette "diğer para
  girişleri" altında görünür.
- `TotalRepayment` kullanıcı girdisi değildir; `MonthlyPayment × InstallmentCount`
  ile türetilir ve en az `Principal` olmalıdır (`TotalRepayment >= Principal`).
- Faiz/KKDF/BSMV kırılımı tutulmaz; otomatik faiz hesabı yoktur.

### 6.2 Taksit planı

- **Tüm vade** için taksit kayıtları üretilir (örn. 36 taksit).
- Taksit tutarları eşittir: her taksit `InstallmentAmount = MonthlyPayment`.
- `TotalRepayment = MonthlyPayment × InstallmentCount` tam çarpımdır; son taksite
  yuvarlama farkı eklenmez. Yuvarlama yalnızca gösterimde 2 haneye yapılır.
- Vade tarihleri `FirstDueDate`'ten başlayarak aylık ilerler; ayın günü yoksa
  (örn. 31 Şubat) ayın son gününe sabitlenir.
- Taksit durumları: `Planned`, `Paid`, `Cancelled`.
- **Planlanan taksit, ödenmiş sayılmaz.** Raporlarda borç olarak görünür; gider
  veya ödeme olarak sayılmaz.

### 6.3 Taksit ödemesi

- v1'de taksitler **sırayla** ödenir; önceki taksit ödenmeden sonraki ödenemez.
- Ödeme tam taksit tutarıdır; kısmi ödeme v1'de yoktur.
- `PaidOn <= bugün`.
- Bir taksit için tek aktif ödeme olabilir.
- Ödeme iptal edilirse taksit tekrar `Planned` olur; borç geri yükselir.
- Taksit ödemesi **gider değildir**; borcu azaltır.

### 6.4 Kalan kredi borcu

```
kalanKrediBorcu = Σ(Planned taksit tutarları)
                = TotalRepayment − Σ(Paid taksit tutarları)
```

- İptal edilen kredi raporlara girmez.
- Vadesi geçmiş `Planned` taksitler de kalan borca dahildir; ayrıca "gecikmiş"
  olarak işaretlenir.

### 6.5 Kişisel borç (PersonalDebt)

| Alan | Kural |
|---|---|
| `CounterpartyName` | Zorunlu; borç alınan kişi/kurum |
| `Principal` | `> 0` |
| `BorrowedOn` | `<= bugün` |
| `DueDate` | Opsiyonel; `>= BorrowedOn` |
| `RemainingAmount` | Türetilir; kullanıcı set edemez |

- Kişisel borç oluşturulduğunda **aynı işlemde** `CashInflow`
  (`Source = PersonalBorrowing`, tutar = `Principal`) oluşturulur.
- Bu para girişi **kazanılmış gelir değildir**.

### 6.6 Kişisel borç geri ödemesi

- `Amount > 0`, `PaidOn <= bugün`.
- Aktif geri ödemeler toplamı `Principal`'ı aşamaz.
- `RemainingAmount = Principal − Σ(aktif geri ödemeler)`.
- `RemainingAmount = 0` olduğunda borç `Closed` olur.
- Geri ödeme iptal edilebilir; kalan tutar geri yükselir.
- Geri ödeme **gider değildir**; borcu azaltır.
- Geri ödeme varken borç iptal edilemez; önce geri ödemeler iptal edilir.

### 6.7 Borç durumları

- `Loan`: `Active`, `Closed`, `Cancelled`.
- `PersonalDebt`: `Active`, `Closed`, `Cancelled`.
- İptal edilen borç ve bağlı para girişi raporlardan çıkar.
- Borç hard delete edilmez.

---

## 7. Raporlama (Reporting)

### 7.1 Dönem

- Varsayılan dönem **takvim ayıdır**: ayın 1. günü 00:00 ile son günü 23:59
  (kullanıcının saat diliminde).
- Kayıtlar `OccurredOn` / `PaidOn` / `RefundedOn` tarihine göre dahil edilir;
  ayın ilk ve son günü dahildir.
- Varsayılan saat dilimi `Europe/Istanbul`; kullanıcı ayarlardan değiştirebilir.
- Gelecek tarihli planlanan taksitler ve ekstre son ödeme tarihleri gelir/gider
  toplamına girmez.

### 7.2 Aylık özet

| Gösterge | Hesap |
|---|---|
| Kazanılmış gelir | Aktif `CashInflow` ve `IsEarnedIncome = true` |
| Diğer para girişleri | Aktif, bilinen ve kazanılmış olmayan kaynaklar (`BankLoan`, `CashAdvance`, `PersonalBorrowing`, `AssetSale`) |
| Belirsiz para girişleri | Aktif `CashInflow` ve `Source = Unknown` |
| Brüt harcama | Aktif `Expense` toplamı |
| İade | Aktif `Refund` toplamı |
| **Net harcama** | Brüt harcama − iade |
| Gelir−gider farkı | Kazanılmış gelir − net harcama |

- **Gelir−gider farkı banka bakiyesi değildir.** Yanıt modelinde
  `isBankBalance: false` ve açıklama alanı bulunur.
- Diğer para girişleri ve belirsiz girişler gelir toplamına katılmaz.
- İade, gerçekleştiği dönemde gideri azaltır; orijinal dönem geriye dönük
  değiştirilmez.

### 7.3 Kategori kırılımı

- Net harcama kategorilere göre gösterilir.
- İade, orijinal harcamanın kategorisinde **negatif gider** olarak yansıtılır;
  iade dönemi esas alınır.
- Arşivlenmiş kategori geçmiş raporlarda görünmeye devam eder.
- Kart alışverişleri de kendi kategorileriyle bu kırılıma girer.

### 7.4 Borç özeti

```
toplamBorç = Σ(max(0, kartBorc))
           + Σ(kalanKrediBorcu)
           + Σ(kalanKişiselBorç)
```

- Nakit avans kart borcunun içinde **bir kez** sayılır; ayrıca eklenmez.
- Negatif kart bakiyesi (kart alacağı) diğer borçlardan düşülmez; ayrı gösterilir.
- Planlanan taksitler borç olarak sayılır; gider olarak sayılmaz.
- Ekstre tutarı borç toplamına eklenmez.
- Kredi ve kişisel borç kaynaklı para girişleri gelir değildir; borç toplamına
  da ikinci kez eklenmez.

### 7.5 Yaklaşan ödemeler

- Önümüzdeki 30 gün içindeki kredi taksitleri ve kart ekstre son ödeme tarihleri
  bilgi amaçlı listelenir.
- Bu liste gider veya borç toplamına yeni kayıt eklemez.

### 7.6 Raporlama sınırları

- Reporting **salt okunurdur**; hiçbir finansal kaydın sahibi değildir.
- Veriyi `ModuleContracts` üzerinden veya kendi okuma modelinden alır; başka
  modülün tablosuna doğrudan SQL atmaz.
- İptal edilen kayıtlar tüm toplamlardan hariçtir.
- Toplamlar stored decimal değerlerle hesaplanır; yalnızca gösterimde 2 haneye
  yuvarlanır.

---

## 8. Modüller arası tutarlılık

| Kural | Açıklama |
|---|---|
| Tek kayıt ilkesi | Kart alışverişi yalnızca `Expense`; ayrı kart satın alma kaydı yok |
| Çift sayım yasağı | Kart ödemesi gider değil; ekstre borç değil; nakit avans tek borç bileşeni |
| Borç-parа girişi eşliği | `BankLoan`, `CashAdvance`, `PersonalBorrowing` girişleri sahibi borç kaydıyla birlikte oluşur/iptal olur |
| Referans bütünlüğü | Modüller arası yalnızca ID referansı; FK yok; doğrulama `ModuleContracts` ile |
| Atomiklik | Yarım kalan kullanıcı işlemi kalıcı olmaz; hata durumunda geri alınır |
| İzolasyon | Her sorgu `UserId` ile filtrelenir; başka kullanıcı verisi görünmez |

> **Uygulama notu:** Borç doğuran para girişleri için senkron yazma sözleşmesi
> veya aynı veritabanı işlemi içinde çalışan bir orkestrasyon kullanılabilir.
> Hangi mekanizmanın seçildiği ilgili dilim tasarımında ADR ile kayda geçirilir;
> iş kuralı değişmez: iki kayıt birlikte oluşur.

---

## 9. Zorunlu regresyon testleri

Aşağıdaki testler CI'da zorunludur; biri kırmızıysa sürüm çıkmaz.

1. Kart borcu ödemesi ikinci kez gider oluşturmaz.
2. Nakit avans toplam borçta bir kez sayılır.
3. Kredi tutarı kazanılmış gelir sayılmaz.
4. Planlanan taksit ödenmiş sayılmaz.
5. Kullanıcı başka kullanıcının kaydını okuyamaz/yazamaz.
6. Kart alışverişi aylık giderde bir kez, kart borcunda bir kez görünür; iki ayrı
   gider kaydı oluşmaz.
7. Kart iadesi hem net harcamayı hem kart borcunu azaltır; gelir oluşturmaz.
8. Ekstre tutarı kart borcunu veya gideri değiştirmez.
9. Devreden ekstre borcu kart borcuna ikinci kez eklenmez.
10. Kredi ve kişisel borç oluşturma, bağlı para girişi olmadan tamamlanmaz.
11. `Unknown` kaynaklı para girişi kazanılmış gelire dahil edilmez.
12. Rapor dönemi saat dilimi sınırında doğru hesaplanır.

---

## 10. Kapatılan açık kararlar

| # | Konu | Karar | Bölüm |
|---|---|---|---|
| 1 | Ekstre alanları | Tarihler + ekstre tutarı manuel; devreden borç türetilir | §5.6 |
| 2 | Nakit avans mutabakatı | Kart borcuna eklenir; ödeme kart ödemesi sayılır; tek sayım | §5.5 |
| 3 | Faiz/ücret | Yalnızca kullanıcı girdisi; otomatik hesap yok | §6.1 |
| 4 | Taksit kapsamı | Tüm vade planı üretilir | §6.2 |
| 5 | Değişiklik geçmişi | Durum + iade/iptal kaydı; tam audit log v1'de yok | §2.3, §3.5 |
| 6 | Rapor dönemi | Takvim ayı + `OccurredOn` + `Europe/Istanbul` | §7.1 |
| 7 | Belirsiz para girişi | Ayrı "Belirsiz" grubu; gelire dahil edilmez | §4.2 |
| 8 | Kart alışverişi sahipliği | `Expense` + `CardId`; CreditCards satın alma kaydı tutmaz | §3.2, ADR-0008 |
| 9 | Ekstre tarih kesiti ve devreden borç | `PeriodEnd` gün sonu; yalnızca aktif ekstreler; `(önceki.PeriodEnd, mevcut.PeriodEnd]` ödemeleri; türetilen alanlar okuma anında; iptal UTC→yerel tarih | §5.3–5.6 |
| 10 | Kredi sözleşmesi | `MonthlyPayment` girdi; `TotalRepayment` türetilir; taksitler eşit | §6.1–6.2 |
| 11 | Bakiye `asOf` ve gelecek tarih | Opsiyonel `asOf`; yoksa bugün; gelecek tarih `400 ValidationFailed` | §5.3–5.4 |

---

## 11. Hâlâ açık olan konular

Aşağıdakiler v1 iş kurallarını bloke etmez; `docs/03` §17'de izlenir:

- `Idempotency-Key`'in v1.1'de zorunlu hale getirilmesi.
- Çoklu para birimi ve kur dönüşümü.
- `Endpoints` projelerinin ayrı kalıp kalmayacağı.
- Parola sıfırlama ve e-posta doğrulama.
- Hesap silme (KVKK) akışı.
- Ekstre belgesi yükleme/OCR ve otomatik içe aktarma (v1.1+).
- Kart harcamaları için taksitli ödeme planı (v1 kapsamı dışı).

---

## 12. Değişiklik geçmişi

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-11-13 | İlk sürüm; doküman 02 §5 açık kararları ve kart alışverişi sahipliği kapatıldı |
| 1.1 | 2026-11-15 | Slice 4 tarih kesiti/iptal/devreden borç kuralları netleştirildi; kredi sözleşmesi `MonthlyPayment` girdisine geçti, taksitler eşitlendi |
| 1.2 | 2026-11-15 | Gelecek `asOf` reddi ve iptal zamanının UTC'den kullanıcı yerel tarihine çevrilmesi harcama/iade/ödeme için tek kurala bağlandı |
