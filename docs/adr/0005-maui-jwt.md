# ADR-0005 — .NET MAUI istemci + JWT kimlik doğrulama

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Mimari doküman §10, §14

## Bağlam

Nihai ürün mobil uygulama. Geliştirme sırasında API tarayıcı/Postman ile test
edilecek, ancak bu nihai ürünün web uygulaması olacağı anlamına gelmiyor.

Mobil teknoloji seçenekleri: .NET MAUI, Flutter, React Native.

## Karar

### İstemci
**.NET MAUI** (Android + iOS), MVVM (`CommunityToolkit.Mvvm`), Shell navigasyon.

Gerekçe:
- Tek dil (C#), backend ile aynı toolchain ve SDK.
- Backend DTO'ları ileride paylaşılabilir (`FinTrack.Contracts`).
- Tek geliştirici için bağlam değiştirme maliyeti sıfır.
- Native performans gerektiren bir ekran yok.

### Kimlik doğrulama
**ASP.NET Core Identity + JWT.**
- Access token 15 dk, refresh token 30 gün (rotating, DB'de hash'li)
- Mobil token'ları `SecureStorage` (Android Keystore / iOS Keychain)
- Sunucu **durumsuz**; her istekte `UserId` claim doğrulanır
- Cookie **kullanılmaz** (mobil istemci için uygun değil)

## Sonuçlar

**Olumlu**
- Tek dil, tek IDE, tek build hattı.
- Token yenileme akışı standart ve test edilebilir.
- Backend MAUI olmadan geliştirilebilir (Postman/Scalar).

**Olumsuz**
- MAUI iş yükü kurulumu gerekir: `dotnet workload install maui`
- Android emülatörden host'a erişim `10.0.2.2` ile yapılır (yaygın tuzak).
- MAUI derleme süreleri XAML nedeniyle yavaş.
- Web istemcisi gerekirse yeniden yazım (API aynı kalır).

## Kararlar

- v1'de **çevrimdışı yazma yok**. Yalnızca okuma önbelleği (SQLite).
- Mobil **güvenilmeyen istemci**: tüm iş kuralları sunucuda tekrar doğrulanır.
- Gerçek kart bilgisi hiçbir katmanda istenmez/saklanmaz.

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Flutter | İki dil; backend ile DTO paylaşımı yok. |
| React Native | JS/TS ekosistemi; ekip C# odaklı. |
| Blazor Hybrid (MAUI içinde) | Web UI mobil deneyimi zayıf; v1 için gereksiz. |
| Cookie auth | Mobil için uygun değil, CSRF karmaşası. |
