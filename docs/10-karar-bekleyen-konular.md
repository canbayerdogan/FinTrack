# FinTrack — Karar Bekleyen Konular

**Sürüm:** 1.2  
**Tarih:** 2026-11-18  
**Durum:** ✅ Kararlar verildi + 1.6/1.10 için **koşullu tasarım onayı alındı** + açık kodlama onayı — **V11-T01 uygulandı** (435/435 test, `docs/11` §9.5)\
**Bağlı belgeler:** [İş Kuralları §2.7](05-is-kurallari.md) · [API Sözleşmeleri §1.4](06-api-sozlesmeleri.md) · [Teknik tasarım v1.3](11-idempotency-teknik-tasarim.md) · [ADR-0010](adr/0010-host-owned-idempotent-writes.md) · [TODO — teknik borç](09-todo.md)

> Bu dosya **kod yazılmadan önce** kapatılması gereken kararları içerir.
> Her maddede bir öneri ve seçenekler var. Yanıtını iki yoldan verebilirsin:
>
> 1. Kısa yol: **“Öneriler onaylandı.”** yazman yeterli; varsa istisnaları belirtirsin.
> 2. Ayrıntılı yol: Her maddedeki `**Kararınız:**` alanını doldurursun (kullanıcı bu yolu seçti).
>
> **Cevaplar `10-karar-bekleyen-konulara-cevap.txt` ile verildi (2026-11-18).**
> Kararlar `docs/05`, `docs/06`, `docs/07`, `docs/08`, `docs/09`'a işlendi;
> teknik tasarım [docs/11 v1.3](11-idempotency-teknik-tasarim.md)'de toplandı.
> Kullanıcı `docs/11-kararlar.txt` ile **altı koşullu tasarım onayı** verdi;
> koşulların karşılanması `docs/11` §9.1'de, dört kritik konunun çözümü §9.2'de.
> Kullanıcı daha sonra `5abb6af` sonrası **açık kodlama onayı** verdi ve
> **V11-T01 uygulandı** (435/435 test, `docs/11` §9.5). Operasyonel: LocalDB
> temizliği tamamlandı; Windows manuel UI smoke ve MAUI head build bekliyor.

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

**Karar: A — Kabul edildi.** Tüm finansal/mutasyon `POST` zorunlu; auth `POST` kapsam dışı. → `docs/05` §2.7, `docs/06` §1.4, `docs/11` §1.1.

### 1.2 Anahtarın kapsamı

**Soru:** Aynı anahtarın çakışıp çakışmadığı hangi alanlara göre belirlenacak?

- **Mevcut sözleşme:** kullanıcı + method + path + anahtar.
- **Öneri:** `userId + HTTP method + route template + route değerleri + anahtar`. Query string kapsama **girmez**; gövde ayrıca 1.3'teki hash ile karşılaştırılır.
- **Seçenekler:**
  - **A)** Öneri olarak yazılan kapsam (öneri).
  - **B)** Query string de dahil edilir.
  - **C)** Kullanıcı bazlı izolasyon kaldırılır, anahtar global kabul edilir (önerilmez; güvenlik riski).

**Karar: A — Kabul edildi.** `userId + method + route template + route değerleri + anahtar`; query string hariç. → `docs/11` §3.1.

### 1.3 Gövde karşılaştırma yöntemi

**Soru:** “Aynı gövde / farklı gövde” nasıl tespit edilecek?

- **Öneri:** JSON'u canonical hale getir (özellik sırasını sırala, boşlukları normalize et) → SHA-256 üret → hash'ı sakla. Ham gövde saklanmaz.
- **Seçenekler:**
  - **A)** Canonical JSON + SHA-256 (öneri; boşluk/sıra farkları yanlış `409` üretmez).
  - **B)** Ham istek baytlarının SHA-256'sı (basit ama istemci serileştirme farklarına duyarlı).
  - **C)** Gövdenin tamamını sakla ve birebir karşılaştır (daha fazla veri, kişisel veri saklama riski).

**Karar: A — Kabul edildi, uygulama yöntemi değişti.** Kullanıcı özel canonicalizer'ı
reddetti: **bakımı sürdürülen, RFC 8785 uyumu doğrulanmış bir kütüphane** zorunlu
(birincil aday `Jcs.Net` 0.1.1) ve kodlamadan önce S26/S27 kapısı geçilmeli.
Ham gövde saklanmaz; decimal sayı alanı sınırı §3.2.1'de. → `docs/11` §3.2.

### 1.4 Saklama yeri ve şema

**Soru:** İdempotency kayıtları nerede tutulacak?

- **Öneri:** Host katmanında yeni bir şema/migration: `shared.IdempotencyKeys`. Modüller bu tabloyu bilmez; middleware endpoint önünde çalışır.
- **Seçenekler:**
  - **A)** `shared` şemasında yeni tablo + migration (öneri; modül sınırı korunur).
  - **B)** Mevcut `identity` şemasına ek tablo (yeni şema açılmaz ama anlamsal karışıklık).
  - **C)** Redis / distributed cache (yeni altyapı bağımlılığı → ADR gerekir).
  - **D)** In-memory cache (tek instance varsayımı; yatay ölçeklemeyi bozar).

**Karar: A, mimari koşulla — Kabul edildi.** Host sahipliğinde `shared.IdempotencyKeys` + ayrı migration; modüller tabloya erişmez, modül DbContext/migration düzeni korunur. → `docs/11` §5.

### 1.5 Saklama süresi ve temizlik

**Soru:** 24 saatlik süre korunacak mı, eski kayıtlar nasıl temizlenecek?

- **Mevcut sözleşme:** 24 saat.
- **Öneri:** 24 saat korunur; süresi dolan kayıtlar erişimde temizlenir (lazy delete). Ayrıca periyodik temizlik job'u **eklenmez** (v1 kapsamı).
- **Seçenekler:**
  - **A)** 24 saat + lazy delete (öneri).
  - **B)** 24 saat + periyodik cleanup job'u.
  - **C)** Farklı süre: ____ saat.

**Karar: A, küçük eklemeyle — Kabul edildi.** 24 saat; background job yok; erişimde silme + saatte bir en fazla 1000 satırlık sınırlı toplu temizlik. → `docs/11` §6.

### 1.6 Eşzamanlılık ve `InProgress`

**Soru:** Aynı anahtarla ikinci istek aynı anda gelirse ne olacak? Süreç çökerse kilit nasıl kurtarılacak?

- **Mevcut sözleşme:** `409 IdempotencyInProgress`.
- **Öneri:** Unique index + kayıt durumu `InProgress` → `Completed`. İkinci istek `409`. `InProgress` kaydı **5 dakikadan** eskiyse yeniden denemeye izin verilir (stale lock kurtarma).
- **Seçenekler:**
  - **A)** 5 dk stale timeout (öneri).
  - **B)** 1 dk stale timeout.
  - **C)** Stale timeout yok; manuel müdahale gerekir.

**Karar: Mevcut A önerisi REDDEDİLDİ; yeni tasarım koşullu olarak ONAYLANDI.**
5 dk stale timeout tek başına yetersiz. Idempotency kaydı ile finansal kayıt
**aynı `DbConnection` ve aynı `DbTransaction` nesnesinde** commit edilir; commit'in
tek sahibi host middleware, handler commit etmez; kalıcı `InProgress` satırı
oluşmaz, stale kurtarma gerekmez. Belirsiz commit sonucunda aynı key ile retry
güvenlidir; bağlantı kopması ayrıca test edilir (S21). Çoklu instance hakemi
`UNIQUE (ScopeHash, Key)` (S22–S24). → `docs/11` §4, **ADR-0010**.
**Kodlama onayı ayrıca alınmalı.**

### 1.7 Auth uç noktaları ve token güvenliği

**Soru:** `/auth/login`, `/auth/register`, `/auth/refresh` gibi token döndüren `POST`lar kapsamda mı?

- **Risk:** İlk yanıtı saklayıp tekrar dönmek, access/refresh token'ları veritabanında tutmak anlamına gelir.
- **Öneri:** Auth `POST`larını idempotency kapsamı dışında bırak; `docs/05` §2.7 ve `docs/06` §1.4'ü “tüm POST” yerine “tüm finansal/mutasyon POST” olarak güncelle.
- **Seçenekler:**
  - **A)** Auth `POST`ları kapsam dışı (öneri; en güvenli).
  - **B)** Auth dahil; token içeren yanıt gövdesi şifrelenerek saklanır (ek kriptografi işi).
  - **C)** Auth dahil; sadece durum kodu saklanır, gövde tekrar döndürülmez (login/refresh için pratik değil).

**Karar: A — Kabul edildi.** `register/login/refresh/logout` kapsam dışı; token içeren yanıtlar kaydedilmez. `docs/05` §2.7 ve `docs/06` §1.4 "tüm POST" ifadesi "finansal/mutasyon POST" olarak düzeltildi.

### 1.8 Hata yanıtlarının tekrarı

**Soru:** İlk istek `4xx`/`5xx` döndüyse bu yanıt da saklanıp tekrar mı döndürülecek?

- **Öneri:** Yalnızca **2xx** yanıtlar saklanır ve tekrar döndürülür. Hatalar normal akışta yeniden üretilir; böylece geçici hata kalıcı yanıt olarak donmaz.
- **Seçenekler:**
  - **A)** Sadece 2xx replay (öneri).
  - **B)** 2xx + 4xx replay, 5xx saklanmaz.
  - **C)** Tüm yanıtlar saklanır (sözleşmedeki “ilk yanıt” ifadesinin birebir yorumu).

**Karar: A — Kabul edildi.** Yalnızca 2xx durum kodu + `Location` başlığı + gövde replay için saklanır; 4xx/5xx kalıcı kayıt oluşturmaz.

### 1.9 Eksik / geçersiz header davranışı

**Soru:** Zorunlu header gelmezse veya UUID formatı bozuksa hangi hata döner?

- **Öneri:** `400 ValidationFailed`, hata alanı `idempotencyKey`. Projede doğrulama hataları zaten bu deseni kullanıyor.
- **Seçenekler:**
  - **A)** `400 ValidationFailed` (öneri).
  - **B)** `422 Unprocessable Entity`.
  - **C)** `409 IdempotencyKeyReused`.

**Karar: A — Kabul edildi.** Eksik/geçersiz UUID/bozuk JSON → `400 ValidationFailed`, hata alanı `idempotencyKey`.

### 1.10 Mobil istemci davranışı

**Soru:** Anahtarı kim üretir, retry ne zaman yapılır, anahtarın yaşam süresi ne kadar?

- **Mevcut durum:** `AuthHandler` yalnızca GET/HEAD/OPTIONS retry yapıyor; POST/PUT retry yok.
- **Öneri:** Her kullanıcı aksiyonu için yeni UUID üretilir. Aynı istek ağ hatası/timeout nedeniyle tekrar edilirse **aynı key** kullanılır. `POST` retry açılır; `PUT` retry kapalı kalır. Key yalnızca işlem süresince bellekte tutulur; uygulama yeniden başlarsa yeni key üretilir.
- **Seçenekler:**
  - **A)** Öneri olarak yazılan davranış (öneri).
  - **B)** Key `SecureStorage`'da kalıcı olur; kullanıcı aksiyonu tamamlanana kadar saklanır.
  - **C)** Mobil tarafta retry hiç açılmaz; sadece API tarafı korunur.

**Karar: B, geliştirilmiş haliyle — Kabul edildi; depolama tasarımı koşullu olarak ONAYLANDI.**
Anahtar + şifreli istek gövdesi uygulama restart'ında korunur; çoklu bekleyen
işlem desteklenir; kesin başarıda yerel kayıt silinir; offline yazma kuyruğuna
dönüşmez. App-private SQLite + AES-256-GCM (96-bit nonce, auth tag, AAD; yalnızca
şifreleme anahtarı `SecureStorage`'da). Mevcut mobil altyapıda yalnızca token
store/Preferences olduğu için yeni structured store gerekli; gerekçe `docs/11`
§7.1'de. `Created` otomatik gönderilmez, `SentUnconfirmed` aynı key/gövde ile
kurtarılır; >24 s için durum sorgusu + `Uncertain`, otomatik yeni UUID **yok**
(R13 düzeltildi). → `docs/11` §7. **Kodlama onayı ayrıca alınmalı.**

### 1.11 Uygulama ve test kapsamı

**Soru:** Tek seferde tüm uç noktalara mı uygulanır, yoksa pilot ile mi başlanır?

- **Öneri:** Host middleware ile tüm kapsam dahili `POST`lara tek seferde uygulanır. Entegrasyon testlerinde en az şu senaryolar olur: aynı anahtar+aynı gövde → aynı yanıt/yeni kayıt yok; aynı anahtar+farklı gövde → `409 IdempotencyKeyReused`; eşzamanlı istek → `409 IdempotencyInProgress`; eksik/geçersiz header → `400`; farklı kullanıcı aynı anahtarı kullanabilir.
- **Seçenekler:**
  - **A)** Tüm kapsam + öneri test seti (öneri).
  - **B)** Önce `/expenses` ve `/cards` ile pilot, sonra genişlet.
  - **C)** Sadece unit test; entegrasyon testi eklenmez (önerilmez).

**Karar: A — Kabul edildi.** Tüm kapsam dahili POST'lar + `docs/11` §4.7
**S1–S29** entegrasyon senaryoları zorunlu (RFC vektörleri, bağlantı kopması,
çoklu instance matrisi, dört modül, >24 h mobil akış dâhil).

### 1.12 Mimari yerleşim

**Soru:** Middleware hangi katmanda duracak?

- **Öneri:** Host (`FinTrack.Api`) katmanında, endpoint'lerden önce çalışan bir middleware. Modül projeleri `Idempotency-Key`'i bilmez.
- **Seçenekler:**
  - **A)** Host middleware (öneri; modül sınırı korunur).
  - **B)** Modül başına filter/endpoint filtresi (tekrar ve sınır ihlali riski).
  - **C)** Paylaşılan kütüphaneye middleware, host kaydeder (A ile mantık aynı, sadece dosya yeri değişir).

**Karar: A — Kabul edildi.** Host middleware; modül sınırları korunur; auth uç noktaları açıkça hariç tutulur. ADR-0009 transaction altyapısı tüm mutasyon POST'larına genişletilir (onay: `docs/11` §9/2).

---

## 2. Operasyonel / temizlik kararları

### 2.1 Windows manuel UI smoke

**Soru:** Windows hedefinde de manuel akış yapılsın mı?

- **Öneri:** Evet, yapılsın. Build 0/0 ama uygulama elle açılmadı; aynı `giriş → harcama → kart → rapor` akışı ~10 dk sürer.
- **Seçenekler:** **A)** Yapılsın (öneri) · **B)** Şimdilik atlanır.

**Karar: A — Kabul edildi.** Windows manuel UI smoke (`giriş → harcama → kart → rapor`) görev kartının doğrulama maddesi olarak eklendi.

### 2.2 LocalDB smoke verileri

**Soru:** `smoke_20261118@example.com`, `Market` kategorisi ve 125,00 TRY harcama kalsın mı?

- **Öneri:** Silinsin; bunlar doğrulama verisi, ürün verisi değil. Repo etkilenmez.
- **Seçenekler:** **A)** Silinsin (öneri) · **B)** Kalsın, lokal geliştirmede kullanılır.

**Karar: A — Kabul edildi ve UYGULANDI (2026-11-18).**
`smoke_20261118@example.com`, `Market` kategorisi, 125,00 TRY harcama ve 7 refresh
token tek transaction'da silindi; doğrulama 0 satır. Diğer kullanıcıların
"Market" kayıtlarına dokunulmadı (uygulama kodu etkilenmedi).

### 2.3 `answers-1.txt` git geçmişi

**Soru:** Dosya yeni commit'te takipten çıkarıldı ama eski commit'te duruyor. Geçmişten temizlensin mi?

- **Öneri:** Dokunulmasın. Geçmişi yeniden yazmak zorunlu force-push gerektirir; faydası riskinden az.
- **Seçenekler:** **A)** Olduğu gibi kalsın (öneri) · **B)** Geçmişten kazınacak (ayrı, yıkıcı işlem).

**Karar: A — Kabul edildi.** Git geçmişine dokunulmayacak; force-push/yeniden yazma yok. `docs/answer*.txt` ignore kuralı korunuyor.

### 2.4 Son CI run'ını dokümanlara eklemek

**Soru:** Ignore_commit'in CI run'ı (`44381807517`) de dokümanlara yazılsın mı?

- **Öneri:** Hayır. Kod düzeltmelerinin doğrulaması `44379256250` ile zaten dokümanlarda. Her docs-only run'ı kovalamak gereksiz commit döngüsü yaratır.
- **Seçenekler:** **A)** Eklenmesin (öneri) · **B)** Eklenir.

**Karar: A — Kabul edildi.** `44381807517` docs-only run'ı dokümanlara eklenmeyecek; kod doğrulaması `44379256250` referansı yeterli.

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

**Karar: A — Kabul edildi.** Idempotency tamamlandıktan sonra v1.1 önceliği **parola sıfırlama + e-posta doğrulama**. Diğer konular TODO'da korunur, ayrıca değerlendirilir.

---

## 4. Verilen cevapların özeti

```text
1.1 Zorunluluk kapsamı: A
1.2 Anahtar kapsamı:    A
1.3 Gövde hash:         A (özel canonicalizer REDDEDİLDİ → RFC 8785 kütüphanesi)
1.4 Saklama yeri:       A (mimari koşulla)
1.5 TTL/temizlik:       A (küçük eklemeyle: 24 s + 30 g tombstone)
1.6 InProgress:         A REDDEDİLDİ → tek transaction tasarımı KOŞULLU ONAYLANDI
1.7 Auth kapsamı:       A
1.8 Hata replay:        A
1.9 Hata kodu:          A (+ 409 IdempotencyExpired)
1.10 Mobil retry:       B (geliştirilmiş; depolama KOŞULLU ONAYLANDI, R13 düzeltildi)
1.11 Test kapsamı:      A (S1–S29)
1.12 Mimari yer:        A (ADR-0010)
2.1 Windows smoke:      A (BEKLİYOR — operatör)
2.2 LocalDB verisi:     A (TAMAMLANDI ✅)
2.3 Git geçmişi:        A
2.4 Son CI run:         A
3. v1.1 öncelik:        A
```

Kaynak: `docs/10-karar-bekleyen-konulara-cevap.txt` (yerel, commit edilmez).

---

## 5. Bu dokümanın akıbeti

- ✅ Bu dosya **Sürüm 1.2** yapıldı; tüm kararlar ve koşullu onaylar yukarıya işlendi.
- ✅ Kararlar bağlayıcı belgelere işlendi: `docs/05` §2.7, `docs/06` §1.4,
  `docs/07` §5.1 görev kartı, `docs/08` §4/§8, `docs/09` teknik borç maddesi.
- ✅ Altı koşullu onay karşılandı: [docs/11 v1.1](11-idempotency-teknik-tasarim.md)
  §9.1; dört kritik konu §9.2; kalan riskler §9.4; [ADR-0010](adr/0010-host-owned-idempotent-writes.md).
- ⏳ **Kodlama onayı ayrı alınacak.** Kullanıcı son tasarımı inceledikten sonra
  açık onay verecek; o onay gelene kadar uygulamaya geçilmez.
- Bu dosya kalıcı karar kaynağı değildir; kapatıldığında `docs/05`–`docs/07`
  ve `docs/11` v1.1 esas alınır.
