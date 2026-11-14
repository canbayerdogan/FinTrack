# ADR-0001 — Modüler monolit, tek dağıtım birimi

**Durum:** Kabul edildi
**Tarih:** 2026-11-13
**İlgili:** Mimari doküman §2, §3

## Bağlam

Ürün tek bir mobil uygulamaya hizmet ediyor. Kullanıcı sayısı ve trafik henüz
bilinmiyor. Buna karşın ürün ilkeleri modüller arasında **kesin sınır** istiyor:
finansal kayıtlar, kartlar, borçlar ve raporlama birbirine karışmamalı.

İki uç yaklaşım vardı:
1. Katmansız tek proje — hızlı başlar, 6 ayda çorbaya döner.
2. Mikroservis — sınırları zorla korur, ama dağıtım/monitoring/ağ maliyeti
   tek geliştirici için orantısız.

## Karar

**Tek uygulama, tek dağıtım birimi** içinde, birbirine derleme zamanında bağımlı
olmayan **iş modülleri** (modular monolith).

- Tek `FinTrack.Api` host'u tüm modülleri barındırır.
- Modüller ayrı projelerdir; birbirlerinin içine referans veremez.
- Modüller arası iletişim `FinTrack.ModuleContracts` arayüzleri veya in-process olaylar.
- Bugün mesaj kuyruğu, ayrı veritabanı, ayrı deployment **yok**.

## Sonuçlar

**Olumlu**
- Tek `dotnet run` ile tüm sistem ayağa kalkar.
- Sınırlar derleyiciyle korunur; yanlış bağımlılık build'i kırar.
- Bir modül ileride ayrı servise taşınabilir (sınırlar zaten çizili).

**Olumsuz / kabul edilen maliyet**
- Tek süreç: bir modüldeki bellek sızıntısı hepsini etkiler.
- Ölçekleme modül bazında değil, uygulama bazında.
- 20+ proje yönetimi (bkz. ADR-0002).

## Alternatifler

| Alternatif | Neden seçilmedi |
|---|---|
| Tek proje + klasörler | Sınırlar disiplinle korunur; 3. ayda ihlal başlar. |
| Mikroservis | Ürün gereksinimi değil; operasyonel maliyet yüksek. |
| Modüler monolit + Kafka/RabbitMQ | İhtiyaç yok; belge kuralı "popüler diye eklenmez". |

## Ne zaman yeniden değerlendirilir

- Bir modülün yükü diğerlerinden 10× fazlaysa (ayrı ölçekleme gerekirse),
- Bir modülün deploy sıklığı diğerlerini bloke ediyorsa,
- Ekip 3+ bağımsız takıma bölünürse.
