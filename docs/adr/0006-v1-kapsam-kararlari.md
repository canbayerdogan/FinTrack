# ADR-0006 — v1 kapsamı: abonelik ve varlık satışı kazancı yok

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Finansal Alan Modeli v0.2 §10, §11

## Bağlam

Finansal Alan Modeli v0.2'de iki kapsam sorusu açık kalmıştı:

1. Abonelik takibi ilk sürümde olacak mı?
2. Varlık (altın vb.) satışında kazanç/maliyet hesabı yapılacak mı?

Her ikisi de modeli büyütüyor ve MVP'yi geciktiriyor.

## Karar

### 1. Abonelik — v1'de YOK
Abonelik takibi 2. sürüme bırakıldı.

Gerekçe: abonelik kendi yaşam döngüsüne sahip ayrı bir iş alanı (fiyat değişikliği,
iptal, askıya alma). v1'in çekirdek değeri harcama + kart + borç + aylık özet.
Abonelik bunlar olmadan da anlamlı; bunlar abonelik olmadan da anlamlı.

**Geçici çözüm:** Kullanıcı abonelik ödemesini normal harcama olarak girebilir.

### 2. Varlık satışı — yalnızca para girişi
`CashInflow.Source = AssetSale` kaydı tutulur. Maliyet, kazanç, varlık envanteri
**hesaplanmaz ve saklanmaz**.

Gerekçe: maliyet takibi ürünü yatırım/portföy takibine kaydırır — bu açıkça
kapsam dışı (Product Vision §6.4).

## Sonuçlar

**Olumlu**
- MVP kapsamı daralır, çıkış hızlanır.
- Model, bu özellikleri sonradan eklemeyi engellemez (yeni modül eklenir).

**Olumsuz**
- Kullanıcı aboneliklerini otomatik takip edemez (v1'de).
- Varlık satışında gerçek kazanç görünmez — bilinçli kabul.

## Ne zaman yeniden değerlendirilir

- Kullanıcı geri bildirimi "aboneliklerim nerede" sorusunu sık soruyorsa,
- v1 çekirdeği (dilim 0–8) tamamlandıysa.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Abonelik v1'de basit kayıt olarak | Yine de yaşam döngüsü kuralları gerektirir; yarım özellik riski. |
| Varlık kazancı hesabı | Ürünü yatırım takibine kaydırır; kapsam dışı. |
