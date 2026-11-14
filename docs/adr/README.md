# Mimari Karar Kayıtları (ADR)

Bir karar burada yazılı değilse, **verilmiş sayılmaz**.
Ajanlar açık kararları kendi varsayımlarıyla kapatamaz.

| # | Karar | Durum |
|---|---|---|
| [0001](0001-modular-monolit.md) | Modüler monolit, tek dağıtım birimi | ✅ Kabul |
| [0002](0002-modul-ici-clean-architecture.md) | Modül içi Clean Architecture | ✅ Kabul |
| [0003](0003-cqrs-mediatorsuz.md) | CQRS, mediator olmadan | ✅ Kabul |
| [0004](0004-sql-server-sema-per-modul.md) | Tek SQL Server DB, modül başına şema | ✅ Kabul |
| [0005](0005-maui-jwt.md) | .NET MAUI istemci + JWT | ✅ Kabul |
| [0006](0006-v1-kapsam-kararlari.md) | v1 kapsamı: abonelik ve varlık kazancı yok | ✅ Kabul |
| [0007](0007-odemeler-sahibi-modulde.md) | Ödemeler yükümlülüğün sahibi modülde yaşar | ✅ Kabul |
| [0008](0008-kart-alisverisi-expenses-sahibi.md) | Kart alışverişi Expenses modülünün sahibidir | ✅ Kabul |

## Şablon

```markdown
# ADR-XXXX — Başlık

**Durum:** Önerildi | Kabul edildi | Reddedildi | Yerine geçildi (ADR-YYYY)
**Tarih:** YYYY-MM-DD

## Bağlam
Hangi problem, hangi kısıtlar.

## Karar
Ne yapıyoruz.

## Sonuçlar
Olumlu / olumsuz / kabul edilen maliyet.

## Alternatifler
Neden seçilmedi.

## Ne zaman yeniden değerlendirilir
Somut tetikleyici.
```
