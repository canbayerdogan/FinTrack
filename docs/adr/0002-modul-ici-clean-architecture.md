# ADR-0002 — Modül içi Clean Architecture

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Mimari doküman §4, §7 · Finansal Alan Modeli kararları

## Bağlam

Finansal Alan Modeli çalışmasında iki seçenek değerlendirildi:

- **A — İş odaklı modüller, modül içi katmanlar:** her modül kendi iş kurallarını,
  uygulama akışını ve veri erişimini taşır.
- **B — Uygulama genelinde ortak katmanlar:** tek `Domain`, tek `Application`,
  tek `Infrastructure`; modüller bu katmanların içinde klasörlenir.

Belgede **A** seçildi. Ancak "Clean Architecture" isteği B gibi anlaşılmaya açıktı.

## Karar

Clean Architecture **her modülün içinde** uygulanır:

```
Modules/<Ad>/
├─ <Ad>.Domain           entity, value object, invariant, domain event
├─ <Ad>.Application      command/query, handler, DTO, port arayüzleri
├─ <Ad>.Infrastructure   EF Core, DbContext, migration, dış adaptörler
└─ <Ad>.Endpoints        minimal API, DI kaydı
```

Bağımlılık yönü her modülde içeri doğrudur: `Endpoints → Application → Domain`,
`Infrastructure → Application` (port uygulaması).

**Global `FinTrack.Domain` / `FinTrack.Application` projeleri oluşturulmaz.**

## Sonuçlar

**Olumlu**
- Bir modülü silmek/tamamını taşımak mümkün (bağımlılık yok).
- Modül ekibine bağımsız çalışma alanı.
- Sınır ihlali derleme hatası verir.

**Olumsuz**
- Proje sayısı yüksek (5 modül × 4 = 20).
- Bazı küçük yardımcı tipler `SharedKernel`'e taşınmak zorunda.

## Sınır: `SharedKernel` şişmemeli

`FinTrack.SharedKernel` yalnızca şunları içerir:
`Entity`, `AggregateRoot`, `ValueObject`, `Money`, `Result`, `Error`, `IDomainEvent`,
`ICurrentUser`, `IUnitOfWork`.

İş kuralı **asla** `SharedKernel`'e konmaz. Şüphede kalırsan modüle koy.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| B — global katmanlar | Modül sınırları zamanla aşınır; belgede reddedildi. |
| Tek proje + klasör | Derleyici koruması yok. |
| Modül başına tek proje | Katman ihlali mümkün; test edilebilirlik düşer. |
