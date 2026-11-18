# FinTrack — Karar Bekleyen Konular

**Sürüm:** 1.0  
**Tarih:** 2026-11-18  
**Durum:** ⏳ Yanıt bekliyor  
**Bağlı belgeler:** [İş Kuralları §2.7](05-is-kurallari.md) · [API Sözleşmeleri §1.4](06-api-sozlesmeleri.md) · [TODO — teknik borç](09-todo.md)

> Bu dosya **kod yazılmadan önce** kapatılması gereken kararları içerir.
> Her maddede bir öneri ve seçenekler var. Yanıtını iki yoldan verebilirsin:
>
> 1. Kısa yol: **“Öneriler onaylandı.”** yazman yeterli; varsa istisnaları belirtirsin.
> 2. Ayrıntılı yol: Her maddedeki `**Kararınız:**` alanını doldurursun.
>
> Cevapları aldıktan sonra kararları `docs/05`, `docs/06`, `docs/07`, `docs/08`,
> `docs/09` üzerine işler, görev kartını açar ve ancak sonra koda başlarım.
> Açık karar varken kod yazmayacağım.

---

## 1. `Idempotency-Key` uygulaması — zorunlu kararlar

Mevcut sözleşme [`docs/06` §1.4](06-api-sozlesmeleri.md)'te var ama uygulama yok.
Mobil istemci bu yüzden `POST`/`PUT` için otomatik retry yapmıyor. Bu bölüm
v1.1 işinin nasıl yapılacağını netleştirir.

### 1.1 Zorunluluk kapsamı

**Soru:** v1.1'de başlık hangi `POST` uç noktalarında zorunlu olacak?

- **Öneri:** Mevcut sözleşmelerde “tüm `POST`” yazıyor; ancak 1.7'deki token güvenliği riski nedeniyle auth `POST`larını kapsam dışında tutulmasını öneriyorum. Sonuç: **tüm finansal/mutasyon `POST`** zorunlu, auth `POST`ları hariç (1.7-A ile uyumlu).
- **Seçenekler:**
  - **A)** Tüm finansal/mutasyon `POST` zorunlu, auth `POST`ları kapsam dışı (öneri; 1.7-A ile uyumlu).
  - **B)** Tüm `POST` zorunlu, auth dahil (mevcut sözleşmenin birebir yorumu).
  - **C)** v1.1'de de opsiyonel kalır, sadece desteklenir.

**Kararınız:** ______________________________________

### 1.2 Anahtarın kapsamı

**Soru:** Aynı anahtarın çakışıp çakışmadığı hangi alanlara göre belirlenacak?

- **Mevcut sözleşme:** kullanıcı + method + path + anahtar.
- **Öneri:** `userId + HTTP method + route template + route değerleri + anahtar`. Query string kapsama **girmez**; gövde ayrıca 1.3'teki hash ile karşılaştırılır.
- **Seçenekler:**
  - **A)** Öneri olarak yazılan kapsam (öneri).
  - **B)** Query string de dahil edilir.
  - **C)** Kullanıcı bazlı izolasyon kaldırılır, anahtar global kabul edilir (önerilmez; güvenlik riski).

**Kararınız:** ______________________________________

### 1.3 Gövde karşılaştırma yöntemi

**Soru:** “Aynı gövde / farklı gövde” nasıl tespit edilecek?

- **Öneri:** JSON'u canonical hale getir (özellik sırasını sırala, boşlukları normalize et) → SHA-256 üret → hash'ı sakla. Ham gövde saklanmaz.
- **Seçenekler:**
  - **A)** Canonical JSON + SHA-256 (öneri; boşluk/sıra farkları yanlış `409` üretmez).
  - **B)** Ham istek baytlarının SHA-256'sı (basit ama istemci serileştirme farklarına duyarlı).
  - **C)** Gövdenin tamamını sakla ve birebir karşılaştır (daha fazla veri, kişisel veri saklama riski).

**Kararınız:** ______________________________________

### 1.4 Saklama yeri ve şema

**Soru:** İdempotency kayıtları nerede tutulacak?

- **Öneri:** Host katmanında yeni bir şema/migration: `shared.IdempotencyKeys`. Modüller bu tabloyu bilmez; middleware endpoint önünde çalışır.
- **Seçenekler:**
  - **A)** `shared` şemasında yeni tablo + migration (öneri; modül sınırı korunur).
  - **B)** Mevcut `identity` şemasına ek tablo (yeni şema açılmaz ama anlamsal karışıklık).
  - **C)** Redis / distributed cache (yeni altyapı bağımlılığı → ADR gerekir).
  - **D)** In-memory cache (tek instance varsayımı; yatay ölçeklemeyi bozar).

**Kararınız:** ______________________________________

### 1.5 Saklama süresi ve temizlik

**Soru:** 24 saatlik süre korunacak mı, eski kayıtlar nasıl temizlenecek?

- **Mevcut sözleşme:** 24 saat.
- **Öneri:** 24 saat korunur; süresi dolan kayıtlar erişimde temizlenir (lazy delete). Ayrıca periyodik temizlik job'u **eklenmez** (v1 kapsamı).
- **Seçenekler:**
  - **A)** 24 saat + lazy delete (öneri).
  - **B)** 24 saat + periyodik cleanup job'u.
  - **C)** Farklı süre: ____ saat.

**Kararınız:** ______________________________________

### 1.6 Eşzamanlılık ve `InProgress`

**Soru:** Aynı anahtarla ikinci istek aynı anda gelirse ne olacak? Süreç çökerse kilit nasıl kurtarılacak?

- **Mevcut sözleşme:** `409 IdempotencyInProgress`.
- **Öneri:** Unique index + kayıt durumu `InProgress` → `Completed`. İkinci istek `409`. `InProgress` kaydı **5 dakikadan** eskiyse yeniden denemeye izin verilir (stale lock kurtarma).
- **Seçenekler:**
  - **A)** 5 dk stale timeout (öneri).
  - **B)** 1 dk stale timeout.
  - **C)** Stale timeout yok; manuel müdahale gerekir.

**Kararınız:** ______________________________________

### 1.7 Auth uç noktaları ve token güvenliği

**Soru:** `/auth/login`, `/auth/register`, `/auth/refresh` gibi token döndüren `POST`lar kapsamda mı?

- **Risk:** İlk yanıtı saklayıp tekrar dönmek, access/refresh token'ları veritabanında tutmak anlamına gelir.
- **Öneri:** Auth `POST`larını idempotency kapsamı dışında bırak; `docs/05` §2.7 ve `docs/06` §1.4'ü “tüm POST” yerine “tüm finansal/mutasyon POST” olarak güncelle.
- **Seçenekler:**
  - **A)** Auth `POST`ları kapsam dışı (öneri; en güvenli).
  - **B)** Auth dahil; token içeren yanıt gövdesi şifrelenerek saklanır (ek kriptografi işi).
  - **C)** Auth dahil; sadece durum kodu saklanır, gövde tekrar döndürülmez (login/refresh için pratik değil).

**Kararınız:** ______________________________________

### 1.8 Hata yanıtlarının tekrarı

**Soru:** İlk istek `4xx`/`5xx` döndüyse bu yanıt da saklanıp tekrar mı döndürülecek?

- **Öneri:** Yalnızca **2xx** yanıtlar saklanır ve tekrar döndürülür. Hatalar normal akışta yeniden üretilir; böylece geçici hata kalıcı yanıt olarak donmaz.
- **Seçenekler:**
  - **A)** Sadece 2xx replay (öneri).
  - **B)** 2xx + 4xx replay, 5xx saklanmaz.
  - **C)** Tüm yanıtlar saklanır (sözleşmedeki “ilk yanıt” ifadesinin birebir yorumu).

**Kararınız:** ______________________________________

### 1.9 Eksik / geçersiz header davranışı

**Soru:** Zorunlu header gelmezse veya UUID formatı bozuksa hangi hata döner?

- **Öneri:** `400 ValidationFailed`, hata alanı `idempotencyKey`. Projede doğrulama hataları zaten bu deseni kullanıyor.
- **Seçenekler:**
  - **A)** `400 ValidationFailed` (öneri).
  - **B)** `422 Unprocessable Entity`.
  - **C)** `409 IdempotencyKeyReused`.

**Kararınız:** ______________________________________

### 1.10 Mobil istemci davranışı

**Soru:** Anahtarı kim üretir, retry ne zaman yapılır, anahtarın yaşam süresi ne kadar?

- **Mevcut durum:** `AuthHandler` yalnızca GET/HEAD/OPTIONS retry yapıyor; POST/PUT retry yok.
- **Öneri:** Her kullanıcı aksiyonu için yeni UUID üretilir. Aynı istek ağ hatası/timeout nedeniyle tekrar edilirse **aynı key** kullanılır. `POST` retry açılır; `PUT` retry kapalı kalır. Key yalnızca işlem süresince bellekte tutulur; uygulama yeniden başlarsa yeni key üretilir.
- **Seçenekler:**
  - **A)** Öneri olarak yazılan davranış (öneri).
  - **B)** Key `SecureStorage`'da kalıcı olur; kullanıcı aksiyonu tamamlanana kadar saklanır.
  - **C)** Mobil tarafta retry hiç açılmaz; sadece API tarafı korunur.

**Kararınız:** ______________________________________

### 1.11 Uygulama ve test kapsamı

**Soru:** Tek seferde tüm uç noktalara mı uygulanır, yoksa pilot ile mi başlanır?

- **Öneri:** Host middleware ile tüm kapsam dahili `POST`lara tek seferde uygulanır. Entegrasyon testlerinde en az şu senaryolar olur: aynı anahtar+aynı gövde → aynı yanıt/yeni kayıt yok; aynı anahtar+farklı gövde → `409 IdempotencyKeyReused`; eşzamanlı istek → `409 IdempotencyInProgress`; eksik/geçersiz header → `400`; farklı kullanıcı aynı anahtarı kullanabilir.
- **Seçenekler:**
  - **A)** Tüm kapsam + öneri test seti (öneri).
  - **B)** Önce `/expenses` ve `/cards` ile pilot, sonra genişlet.
  - **C)** Sadece unit test; entegrasyon testi eklenmez (önerilmez).

**Kararınız:** ______________________________________

### 1.12 Mimari yerleşim

**Soru:** Middleware hangi katmanda duracak?

- **Öneri:** Host (`FinTrack.Api`) katmanında, endpoint'lerden önce çalışan bir middleware. Modül projeleri `Idempotency-Key`'i bilmez.
- **Seçenekler:**
  - **A)** Host middleware (öneri; modül sınırı korunur).
  - **B)** Modül başına filter/endpoint filtresi (tekrar ve sınır ihlali riski).
  - **C)** Paylaşılan kütüphaneye middleware, host kaydeder (A ile mantık aynı, sadece dosya yeri değişir).

**Kararınız:** ______________________________________

---

## 2. Operasyonel / temizlik kararları

### 2.1 Windows manuel UI smoke

**Soru:** Windows hedefinde de manuel akış yapılsın mı?

- **Öneri:** Evet, yapılsın. Build 0/0 ama uygulama elle açılmadı; aynı `giriş → harcama → kart → rapor` akışı ~10 dk sürer.
- **Seçenekler:** **A)** Yapılsın (öneri) · **B)** Şimdilik atlanır.

**Kararınız:** ______________________________________

### 2.2 LocalDB smoke verileri

**Soru:** `smoke_20261118@example.com`, `Market` kategorisi ve 125,00 TRY harcama kalsın mı?

- **Öneri:** Silinsin; bunlar doğrulama verisi, ürün verisi değil. Repo etkilenmez.
- **Seçenekler:** **A)** Silinsin (öneri) · **B)** Kalsın, lokal geliştirmede kullanılır.

**Kararınız:** ______________________________________

### 2.3 `answers-1.txt` git geçmişi

**Soru:** Dosya yeni commit'te takipten çıkarıldı ama eski commit'te duruyor. Geçmişten temizlensin mi?

- **Öneri:** Dokunulmasın. Geçmişi yeniden yazmak zorunlu force-push gerektirir; faydası riskinden az.
- **Seçenekler:** **A)** Olduğu gibi kalsın (öneri) · **B)** Geçmişten kazınacak (ayrı, yıkıcı işlem).

**Kararınız:** ______________________________________

### 2.4 Son CI run'ını dokümanlara eklemek

**Soru:** Ignore_commit'in CI run'ı (`44381807517`) de dokümanlara yazılsın mı?

- **Öneri:** Hayır. Kod düzeltmelerinin doğrulaması `44379256250` ile zaten dokümanlarda. Her docs-only run'ı kovalamak gereksiz commit döngüsü yaratır.
- **Seçenekler:** **A)** Eklenmesin (öneri) · **B)** Eklenir.

**Kararınız:** ______________________________________

---

## 3. v1.1 önceliği

**Soru:** `Idempotency-Key` bittikten sonra sıradaki konu ne olsun?

- **Öneri:** Henüz seçilmedi. Aşağıdan bir tane seçmen yeterli; seçim yapılana kadar v1.1'e başlamam.
- **Seçenekler:**
  - **A)** Parola sıfırlama + e-posta doğrulama
  - **B)** Hesap silme (KVKK / soft delete + 30 gün)
  - **C)** Rate limiting / kota
  - **D)** OpenTelemetry / gözlemlenebilirlik
  - **E)** Ekstre belgesi yükleme / OCR
  - **F)** Diğer: __________________

**Kararınız:** ______________________________________

---

## 4. Hızlı cevap özeti (istersen sadece burayı doldur)

```text
1.1 Zorunluluk kapsamı: A / B / C
1.2 Anahtar kapsamı:    A / B / C
1.3 Gövde hash:         A / B / C
1.4 Saklama yeri:       A / B / C / D
1.5 TTL/temizlik:       A / B / C
1.6 InProgress:         A / B / C
1.7 Auth kapsamı:       A / B / C
1.8 Hata replay:        A / B / C
1.9 Hata kodu:          A / B / C
1.10 Mobil retry:       A / B / C
1.11 Test kapsamı:      A / B / C
1.12 Mimari yer:        A / B / C
2.1 Windows smoke:      A / B
2.2 LocalDB verisi:     A / B
2.3 Git geçmişi:        A / B
2.4 Son CI run:         A / B
3. v1.1 öncelik:        A / B / C / D / E / F
```

---

## 5. Bu dokümanın akıbeti

- Yanıtları aldıktan sonra bu dosya **Sürüm 1.0 → 1.1** yapılır ve kararlar yazılır.
- Kararlar bağlayıcı belgelere işlenir: `docs/05` §2.7, `docs/06` §1.4, `docs/07` (yeni görev kartı), `docs/08` §4/§8, `docs/09` teknik borç maddesi.
- Bu dosya kalıcı bir karar kaynağı değil, **oturumluk karar talebidir**; kapatıldığında `docs/05`–`docs/07` esas alınır.
