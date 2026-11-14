# Finansal Alan Modeli — Kararlar (v0.4)

**Sürüm:** 0.4
**Bağlı belgeler:** Product Vision & Scope · Finansal Alan Modeli v0.2 (kaynak metinde) · [İş Kuralları v1.0](05-is-kurallari.md)
**Amaç:** v0.2 sonrası verilen kararları kayda geçirmek ve kavramları modül sahipliğine bağlamak.

> v0.2'nin tam metni `_source/gemini-chatgpt-konusma.txt` içinde duruyor.
> Bu belge yalnızca **değişen ve eklenen** kararları içerir; v0.2 ile çelişmez.

---

## 1. v0.2'de kesinleşen kararlar (korunuyor)

| # | Karar |
|---|---|
| K1 | İlk sürümde harcama ve borçla birlikte **temel para girişi** kaydı bulunur |
| K2 | Kredi kartı borcu **işlem ve ödemelerden hesaplanır**; ekstre dönemsel özdür |
| K3 | Nakit avans **ayrı ama kart borcuyla ilişkili** borçtur; çift sayım engellenir |

---

## 2. v0.3 ve v0.4'te eklenen kararlar

### K4 — Varlık satışında kazanç hesabı yok

Altın veya benzeri bir varlık satıldığında:

- ✅ `CashInflow` kaydı oluşur, `Source = AssetSale`
- ✅ Para girişi aylık özette **"diğer para girişleri"** altında görünür
- ❌ Alış maliyeti sorulmaz
- ❌ Kazanç/zarar hesaplanmaz
- ❌ Varlık envanteri tutulmaz

**Gerekçe:** maliyet takibi ürünü portföy/yatırım takibine kaydırır — kapsam dışı.

### K5 — Abonelik v1 kapsamında değil

Abonelik kendi yaşam döngüsüne sahip ayrı bir iş alanı olarak **2. sürüme** bırakıldı.
v1'de abonelik ödemesi normal harcama olarak girilebilir.

### K6 — Ödeme, yükümlülüğün sahibi olan modülde yaşar

v0.2'de "para hareketleri ve ödemeler" tek bir iş alanı olarak önerilmişti.
v0.3'te bu ayrıştırıldı:

> **Bir yükümlülüğün ödemesi, o yükümlülüğe sahip modülde tutulur.**

| Ödeme türü | Sahip modül |
|---|---|
| Kart borcu ödemesi | CreditCards |
| Nakit avans geri ödemesi | CreditCards |
| Kredi taksiti | Debts |
| Kişisel borç geri ödemesi | Debts |

**Gerekçe:**
- Çift sayım riski, borç bakiyesinin tutulduğu yerden uzaklaştıkça artar.
- Her ödeme, sahibi olduğu bakiyeyi **aynı transaction içinde** güncellemelidir.
- Merkezî "ödemeler" modülü, tüm borç türlerini tanımak zorunda kalır → god-module.

**Sonuç:** `CashInflows` modülü yalnızca **para girişini** yönetir, ödeme yönetmez.

### K7 — Modül sahipliği

| Kavram | Sahip modül | Not |
|---|---|---|
| Harcama, **kart alışverişi** (`Expense` + `CardId`), iade, kategori | **Expenses** | Kart alışverişinin tek doğruluk kaynağı (K8) |
| Para girişi, kaynak türü | **CashInflows** | `IsEarnedIncome` türetilir |
| Kart tanımı, kart ödemesi, ekstre, kart borcu, nakit avans | **CreditCards** | Borç hesaplanır, saklanmaz |
| Kredi, taksit planı, kişisel borç, geri ödeme | **Debts** | |
| Aylık özet, borç durumu, kategori kırılımı | **Reporting** | Veri sahibi değil |

> Kart alışverişi için **ayrı bir "kart işlemi" kaydı yoktur**. CreditCards, kart
> borcunu hesaplarken Expenses'taki harcama/iade kayıtlarını `ModuleContracts`
> üzerinden okur (ADR-0008).

### K8 — Kart alışverişi Expenses modülünün sahibidir

Kredi kartıyla yapılan alışveriş:

- ✅ Tek bir `Expense` kaydıdır; `PaymentMethod = CreditCard` ve zorunlu `CardId` taşır
- ✅ Kategori, açıklama, iade ve iptal kuralları Expenses'a tabidir
- ✅ Aylık gider raporunda **bir kez** görünür
- ✅ Kart borcunda da **aynı kayıttan** hesaplanır; ikinci bir gider/borç kaydı yoktur
- ❌ CreditCards modülünde ayrı `CardTransaction`/satın alma kaydı tutulmaz

**Gerekçe:** İki kayıt iki doğruluk kaynağı doğurur; "aynı ekonomik olay iki kez
sayılmaz" kuralı ancak tek kayıtla yapısal olarak korunabilir. Ayrıntı: ADR-0008.

---

## 3. Para girişi sınıflandırması (v0.2 §1.1'in kesinleşmiş hâli)

Her para girişi **iki** bilgi taşır: `Source` (nereden) ve türetilmiş `IsEarnedIncome` (ne anlama).

| Kaynak | `IsEarnedIncome` | Borç etkisi |
|---|---|---|
| `Salary` | ✅ true | Yok |
| `BusinessIncome` | ✅ true | Yok |
| `BankLoan` | ❌ false | Debts modülünde `Loan` oluşur |
| `CashAdvance` | ❌ false | CreditCards modülünde `CashAdvance` oluşur |
| `PersonalBorrowing` | ❌ false | Debts modülünde `PersonalDebt` oluşur |
| `AssetSale` | ❌ false | Yok |
| `Unknown` | ⚠️ belirsiz | Belirsiz kalır, tahmin edilmez |

**Kurallar (v0.2 Kural A–D, korunuyor):**
- **A** — Para girişi gelir demek değildir.
- **B** — Borçlanma gelir oluşturmaz.
- **C** — İade yeni kazanç olarak raporlanmaz; ilgili harcamayı azaltır.
- **D** — Belirsiz kaynak otomatik tahmin edilmez; AI da kesinleştirmez.

> `IsEarnedIncome` **kullanıcı tarafından set edilemez**. `Source`'tan türetilir.
> Bu, v0.2'deki en kritik kuralın teknik güvencesidir.

---

## 4. Aylık özet örneği (v0.2 §1.3, doğrulanmış)

Girdiler: 80.000 maaş · 30.000 kredi · 5.000 nakit avans · 2.000 iade · 50.000 harcama

| Gösterge | Tutar | Not |
|---|---|---|
| Kazanılmış gelir | 80.000 TL | Yalnızca `IsEarnedIncome = true` |
| Diğer para girişleri — kredi | 30.000 TL | Gelir değil |
| Diğer para girişleri — nakit avans | 5.000 TL | Gelir değil, borç |
| İade | 2.000 TL | Harcamayı azaltır |
| Harcamalar | 50.000 brüt → **48.000 net** | |
| Toplam borç | kredi + kart (nakit avans **dahil, bir kez**) | Çift sayım yok |

Rapor **banka bakiyesi iddiası taşımaz**.

---

## 5. Açık konular — kapatıldı

v0.2 §10'daki açık maddeler, v0.3'te kalan yedi madde ve kart alışverişi
sahipliği **2026-11-13'te kapatılmıştır**. Bağlayıcı ayrıntı:
[İş Kuralları v1.0](05-is-kurallari.md) §10.

| # | Konu | Karar | İş kuralları |
|---|---|---|---|
| 1 | Ekstre: hangi alanlar manuel girilir? | Tarihler + ekstre tutarı manuel; devreden borç türetilir | §5.6 |
| 2 | Nakit avansın kart borcuyla mutabakatı | Kart borcuna eklenir; geri ödemesi kart ödemesidir; tek sayım | §5.5 |
| 3 | Faiz/ücret | Yalnızca kullanıcı girdisi; otomatik hesap yok | §6.1 |
| 4 | Kredi taksitlerinin kapsamı | Tüm vade için taksit planı üretilir | §6.2 |
| 5 | İade/düzeltmede değişiklik geçmişi | Durum + iade/iptal kaydı; tam audit log v1'de yok | §2.3, §3.5 |
| 6 | Aylık rapor dönemi | Takvim ayı, `OccurredOn`, varsayılan `Europe/Istanbul` | §7.1 |
| 7 | Belirsiz para girişinin gösterimi | Ayrı "Belirsiz" grubu; kazanılmış gelire dahil edilmez | §4.2 |
| 8 | Kart alışverişi sahipliği | `Expense` + `CardId`; CreditCards satın alma kaydı tutmaz (K8) | §3.2 |

Ek olarak kapananlar: ~~gelir sınıflandırması~~ (K7), ~~abonelikler~~ (K5),
~~varlık satışı~~ (K4).

> Veritabanı şeması ve sınıf tasarımı artık bu kararlara göre kesinleştirilebilir.
> Kuralda boşluk çıkarsa ajan kendi varsayımıyla kapatamaz; önce sorar.
