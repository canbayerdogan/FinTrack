# ADR-0007 — Ödemeler yükümlülüğün sahibi modülde yaşar

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Finansal Alan Modeli kararlar §K6 · Mimari doküman §5

## Bağlam

Finansal Alan Modeli v0.2, "Para hareketleri ve ödemeler" adında **tek bir iş alanı**
öneriyordu: para girişleri, çıkışları ve borç ödemelerinin kaydı.

Bu yaklaşımın sorunu: böyle bir modül, kredi kartı borcunu, kredi taksitini ve
kişisel borcu **aynı anda** tanımak zorunda kalır. Üçünün yaşam döngüsü farklı
(v0.2 §2 kabul ediyor), ama ödeme kaydı üçünü de bilmek zorunda.

Ayrıca ürünün en kritik kuralı şu: **aynı ekonomik olay iki kez gider sayılmaz.**
Borç bakiyesi ile onu azaltan ödeme aynı yerde değilse, bu kuralın korunması
imkânsızlaşır.

## Karar

> **Bir yükümlülüğün ödemesi, o yükümlülüğe sahip modülde tutulur.**

| Ödeme | Sahip modül |
|---|---|
| Kart borcu ödemesi | CreditCards |
| Nakit avans geri ödemesi | CreditCards |
| Kredi taksiti | Debts |
| Kişisel borç geri ödemesi | Debts |

`CashInflows` modülü **yalnızca para girişini** yönetir. Ödeme kaydı tutmaz.

**Sonuç:** 5 modül. Merkezî "Para Hareketleri ve Ödemeler" modülü **yoktur**.

## Sonuçlar

**Olumlu**
- Bakiye ve onu azaltan ödeme aynı aggregate sınırında → çift sayım imkânsız.
- Ödeme + bakiye güncellemesi tek transaction'da.
- Modül, başka modülün borç türlerini tanımak zorunda değil.
- `CashInflows` tek sorumluluklu, küçük kalır.

**Olumsuz**
- "Bu ay yaptığım tüm ödemeler" listesi tek modülden gelmez; Reporting birleştirir.
  Kabul edilen maliyet: raporlama zaten birleştirme katmanıdır.
- Kira gibi **yükümlülüğü olmayan** ödemeler için sahip belirsizdir.

## Kira ve benzeri ödemeler

Kira, fatura gibi önceden tanımlı bir borç kaydı olmayan ödemeler:

- v1'de **Expenses** modülünde harcama olarak kaydedilir (kategori: Kira / Fatura).
- Gelecekte "tekrarlayan yükümlülük" modeli gelirse kendi modülünü alır.

Bu, `Expenses` modülünü bir "ödeme modülü" yapmaz; kira gerçekten bir tüketim gideridir.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Merkezî Para Hareketleri + Ödemeler modülü | God-module; tüm borç türlerini tanır; çift sayım riski yüksek. |
| Ödemeyi hem borç modülünde hem merkezî modülde tutmak | İki doğruluk kaynağı = kaçınılmaz tutarsızlık. |

## Ne zaman yeniden değerlendirilir

- Kira/fatura gibi tekrarlayan yükümlülükler modellenirse (yeni modül, mevcut karar bozulmaz),
- Çapraz modül ödeme listesi performans sorunu olursa (Reporting projeksiyonu eklenir).
