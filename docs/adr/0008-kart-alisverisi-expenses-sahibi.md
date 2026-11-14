# ADR-0008 — Kart alışverişi Expenses modülünün sahibidir

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Finansal Alan Modeli §K7, §K8 · Mimari doküman §5.1, §5.3 · İş Kuralları §3.2, §5.2, §5.3

## Bağlam

İki belge birbiriyle çelişiyordu:

- `docs/02` §K7, "kart tanımı, **kart işlemi**, ekstre, kart borcu, nakit avans"
  kavramlarının tamamını **CreditCards** modülüne veriyordu.
- `docs/03` §5.1 ise `Expense` aggregate'inde opsiyonel `CardId` taşıyor ve
  "harcama her zaman giderdir" diyordu.

Aynı kredi kartı alışverişi iki yerde tutulursa iki doğruluk kaynağı doğar:
aylık gider raporunda bir kez, kart borcunda bir kez sayılması garanti edilemez.
Bu, ürünün en kritik kuralını (**aynı ekonomik olay iki kez sayılmaz**) doğrudan
tehdit eder.

Alışverişin doğası gereği taşıdığı bilgiler — tutar, tarih, kategori, açıklama —
Expenses modülünün zaten sahip olduğu alanlardır. CreditCards yalnızca "hangi
kartla" bilgisine ve borç hesabına ihtiyaç duyar.

## Karar

> **Kredi kartıyla yapılan alışveriş bir `Expense` kaydıdır. Kayıt Expenses
> modülünde yaşar ve opsiyonel `CardId` ile karta referans verir. CreditCards
> modülü ayrı bir satın alma/kart işlemi kaydı tutmaz.**

Sahiplik tablosu:

| Kavram | Sahip | Not |
|---|---|---|
| Kart alışverişi (`Expense` + `CardId`) | **Expenses** | Tek doğruluk kaynağı |
| Kart alışverişi iadesi (`Refund`) | **Expenses** | Aynı harcamaya bağlı |
| Kart tanımı | CreditCards | |
| Kart ödemesi (`CardPayment`) | CreditCards | Gider değildir |
| Nakit avans (`CashAdvance`) | CreditCards | Kart borcu bileşeni |
| Ekstre (`Statement`) | CreditCards | Dönemsel özet; yeni borç değil |
| Hesaplanan kart borcu | CreditCards | Saklanmaz, hesaplanır |

CreditCards, kart borcunu hesaplamak için harcama ve iadeleri
`ModuleContracts` üzerinden (`ICardExpenseQuery`) **okur**; Expenses tablosuna
doğrudan erişemez, modüller arası FK/join kullanamaz.

## Sonuçlar

**Olumlu**
- Kart alışverişi için tek kayıt ve tek doğruluk kaynağı vardır.
- Aylık gider ile kart borcu aynı kayıttan türetilir; çift sayım yapısal olarak engellenir.
- Kategori, açıklama, iade ve düzeltme kuralları tek yerde (Expenses) yaşar.
- Kart numarası/CVV gibi hassas veri yine hiçbir modülde tutulmaz.

**Olumsuz**
- CreditCards'ın borç hesabı, Expenses'a senkron sorgu bağımlılığı taşır.
  Kabul edilen maliyet: sorgu dar ve okuma amaçlıdır (`ICardExpenseQuery`).
- Kart borcu anlık hesaplanırken modüller arası çağrı gerekir; performans için
  snapshot/projeksiyon kullanılabilir, ancak kaynak doğruluk Expenses'tır.
- Kart harcaması da diğer harcamalar gibi silinmez; iptal/iade Expenses
  kurallarına tabidir ve CreditCards bunu okuma tarafında görür.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Alışverişi CreditCards'ta `CardTransaction` olarak tutmak, gideri ayrıca Expenses'a yazmak | İki kayıt = iki doğruluk kaynağı; çift sayım riski. |
| Alışverişi yalnızca CreditCards'ta tutup raporlamaya oradan yansıtmak | Kategori/açıklama/iade yaşam döngüsü Expenses'ta; iki yerde bölünmüş model. |
| Merkezî "işlem" tablosu | Ürün kuralı: harcama, ödeme, para girişi ve borç ayrı kavramlardır. |

## Ne zaman yeniden değerlendirilir

- Kart harcamaları için taksitli ödeme planı v1 kapsamına alınırsa (alışveriş
  başına ayrı plan aggregate'i gerekebilir),
- `ICardExpenseQuery` kart borcu hesabında ölçülebilir performans sorunu yaratırsa
  (CreditCards tarafında okuma projeksiyonu/snapshot eklenir; sahiplik değişmez),
- Banka API/ekstre içe aktarma gelirse (dış kaynaklı hareketlerin eşleştirilmesi
  ayrı bir karar gerektirir).
