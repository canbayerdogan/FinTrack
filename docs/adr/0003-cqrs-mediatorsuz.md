# ADR-0003 — CQRS, mediator olmadan

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Mimari doküman §8

## Bağlam

Kullanıcı "Clean Architecture / CQRS" istedi. Sektörde CQRS denince akla gelen
ilk araç `MediatR`. Ancak ürün belgesi açık bir kural koyuyor:

> "Belirli bir kütüphane, mimari desen veya teknoloji, yalnızca popüler olduğu için
> zorunlu hale getirilmemelidir."

Bu ölçekte (tek host, ~60 handler) MediatR'ın sağladığı tek şey bir sözlük araması
ve pipeline soyutlaması. Handler'lar zaten DI konteynerinde çözülebiliyor.

## Karar

CQRS **mantıksal** uygulanır:

- `ICommand<TResult>` / `IQuery<TResult>` işaretleyici arayüzler
- `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`
- Command → Domain aggregate üzerinden yazar
- Query → EF Core projeksiyonu ile **doğrudan DTO** döner (aggregate yüklemez)
- Endpoint'ler handler'ı DI ile doğrudan enjekte eder

**MediatR, AutoMapper, MassTransit kullanılmaz.**

```csharp
app.MapPost("/api/v1/expenses",
    async (CreateExpenseCommand cmd,
           ICommandHandler<CreateExpenseCommand, Guid> handler,
           CancellationToken ct) => { ... });
```

## Sonuçlar

**Olumlu**
- Handler çağrısı derleme zamanında görünür; "bu kodu kim çağırıyor?" sorusu net.
- Debug stack trace kısa.
- Ek bağımlılık yok; sürüm kırılması riski yok.

**Olumsuz**
- Pipeline davranışları (loglama, validation, transaction) elle kurulur.
  Çözüm: endpoint filter + açık decorator (`ValidatingHandler<T>`).
- Handler'ları toplu dolaşmak için reflection tabanlı tarama yok (gerek de yok).

## Ne zaman yeniden değerlendirilir

Aşağıdakilerden **ikisi** birlikte gerçekleşirse bu ADR yeniden açılır:

1. Aynı pipeline davranışı 10+ handler'da elle tekrar ediyorsa,
2. Modüller arası event dağıtımı 5+ aboneye çıktıysa,
3. Handler sayısı 80'i geçtiyse.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| MediatR + pipeline | Fayda/maliyet oranı bu ölçekte düşük; belge kuralına aykırı. |
| CQRS'siz klasik servis katmanı | Okuma/yazma ihtiyaçları farklı; DTO projeksiyonu kaybolur. |
| Event sourcing | Aşırı karmaşıklık; ürün gereksinimi değil. |
