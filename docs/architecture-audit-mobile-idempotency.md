# FinTrack — Mobil Mutasyon ve Idempotency Entegrasyonu Denetimi

**Tarih:** 2026-11-20\
**Kapsam:** `src/Mobile/FinTrack.Mobile.Core`, `src/Mobile/FinTrack.Mobile`, `tests/FinTrack.Mobile.Tests` ve doğrulama bağlamı için `src/Host/FinTrack.Api/Idempotency/*` kaynak kodu incelemesi.\
**Sınır:** Sadece inceleme ve raporlama. Kod, test, özellik, refactoring, commit veya push yapılmadı. Varsayımlar doğrulanmış gerçek olarak sunulmadı.

## 1. Yönetici özeti (doğrulanmış)

- V11-T01 ile eklenen altyapı (`PendingOperationStore`, `PendingOperationRecoveryService`, `AuthHandler` içindeki koşullu POST retry) **mevcuttur, DI'a kayıtlıdır ve mobil birim testleriyle kısmen doğrulanmıştır**.
- Bu altyapı, incelenen gerçek ViewModel/typed-client yazma akışlarına **entegre edilmemiştir**. Kaynak kodda `PendingOperationRecoveryService.CreateAsync/SendAsync/RecoverAsync` çağrısı, ViewModel/uygulama akışında `IPendingOperationStore` kullanımı ve mutasyon isteklerinde `Idempotency-Key` başlığı üretimi/eklenmesi **bulunmadı**.
- Uygulama başlangıcında recovery tetiklenmez: `App.OnStart` yalnızca oturum durumuna göre yönlendirme yapar; `MauiProgram` recovery servisini singleton olarak kaydeder ancak hiçbir bileşen onu resolve edip çalıştırmaz.
- Sonuç: **Idempotency-Key ve PendingOperation altyapısı, gerçek mobil finansal mutasyon akışlarında kullanılmıyor** (kaynak-kod kanıtı). Altyapı "hazır ama tüketilmeyen" durumdadır.

## 2. ViewModel taraması ve tüm POST/PUT/mutasyon çağrıları

Taranan ViewModel'ler: `AddExpenseViewModel`, `CardDetailViewModel`, `CardsViewModel`, `DashboardViewModel`, `DebtsViewModel`, `DisplayModels`, `ExpensesViewModel`, `LoginViewModel`, `RegisterViewModel`, `ReportsViewModel`, `SettingsViewModel`, `ViewModelBase` (`src/Mobile/FinTrack.Mobile.Core/ViewModels/`).

| ViewModel | Çağrı / eylem | HTTP / hedef | API Client metodu | Mutasyon türü | Finansal mutasyon mu? | Idempotency/Pending entegrasyonu |
|---|---|---|---|---|---|---|
| `AddExpenseViewModel` | `SaveAsync` → `LoadAsync` sadece GET | `POST expenses` (`/api/v1/expenses`) | `FinTrackApiClient.CreateExpenseAsync` | Oluşturma (create) | **Evet** (harcama kaydı) | **Yok** |
| `SettingsViewModel` | `SaveAsync` | `PUT auth/me` (`/api/v1/auth/me`) | `FinTrackApiClient.UpdateProfileAsync` | Güncelleme (profil) | Hayır (profil/saat dilimi) | **Yok** |
| `SettingsViewModel` | `LogoutAsync` | `POST auth/logout` (dolaylı) | `SessionService.LogoutAsync` → `FinTrackApiClient.LogoutAsync` | Oturum kapatma | Hayır | **Yok** |
| `LoginViewModel` | `LoginAsync` | `POST auth/login` (dolaylı) | `SessionService.LoginAsync` → `FinTrackApiClient.LoginAsync` | Auth | Hayır | **Yok** (auth kapsam dışı) |
| `RegisterViewModel` | `RegisterAsync` | `POST auth/register` (dolaylı) | `SessionService.RegisterAsync` → `FinTrackApiClient.RegisterAsync` | Auth | Hayır | **Yok** (auth kapsam dışı) |
| Diğer ViewModel'ler (`Dashboard`, `Expenses`, `Cards`, `CardDetail`, `Debts`, `Reports`) | Listeleme/yükleme | GET istekleri | `Get*Async` metotları | Okuma | Hayır | Uygulanamaz (okuma) |

**Kanıt (ViewModel/Client):**
- `AddExpenseViewModel.cs:80-106` — `SaveAsync` doğrulama sonrası doğrudan `await _api.CreateExpenseAsync(request!, cancellationToken);` çağırır (`:91`), başarıda `//expenses`'e gider (`:92`). Pending kayıt, anahtar, durum veya recovery çağrısı yok.
- `SettingsViewModel.cs:56-92` — `SaveAsync` `await _api.UpdateProfileAsync(PreferredTimeZone.Trim(), RowVersion, cancellationToken);` çağırır (`:73`).
- `SettingsViewModel.cs:95-107` — logout `SessionService.LogoutAsync`.
- `LoginViewModel.cs` / `RegisterViewModel.cs` — `SessionService.LoginAsync/RegisterAsync` kullanır.
- `FinTrackApiClient.cs:20-53` — auth POST'ları; `:46-53` profil PUT; `:77-78` `CreateExpenseAsync` POST `expenses`; `:121-137` POST/PUT yardımcıları `JsonContent` oluşturur, `Idempotency-Key` eklemez; `:139-151` `SendAsync` gövde/başlık manipülasyonu yapmadan gönderir.

## 3. Typed client ve mutasyon envanteri (tam liste)

| Client metodu | HTTP | Yol | Body | `Idempotency-Key` ekleniyor mu? | PendingOperation oluşturuluyor mu? | Not |
|---|---|---|---|---|---|---|
| `LoginAsync` | POST | `auth/login` | `LoginRequest` | Hayır | Hayır | Auth; sunucu idempotency kapsamı dışı (`IdempotencyMiddleware.cs:385-396` auth path hariç) |
| `RegisterAsync` | POST | `auth/register` | `RegisterRequest` | Hayır | Hayır | Auth kapsam dışı |
| `LogoutAsync` | POST | `auth/logout` | `RefreshRequest` | Hayır | Hayır | Auth kapsam dışı |
| `UpdateProfileAsync` | PUT | `auth/me` | `UpdateProfileRequest` | Hayır | Hayır | PUT; middleware sadece POST'ları kapsar (`IsPost` kontrolü) |
| `CreateExpenseAsync` | POST | `expenses` | `CreateExpenseRequest` | Hayır | Hayır | **Finansal mutasyon (gerçek akışta kullanılan tek VM-initiated write).** Sunucu tarafında `/api/v1/expenses` in-scope prefix (`IdempotencyMiddleware.cs:20-28`, `:390-396`) |

Doğrulanmış ek bulgu: `IdempotencyMiddleware` in-scope POST için geçerli UUID `Idempotency-Key` ister; eksik/geçersizse `400 ValidationFailed` (`idempotencyKey`) döner (`IdempotencyMiddleware.cs:60-68`, `IdempotencyProblems.cs:15-22`, `IdempotencyScope.cs:11`). Bu, mevcut `CreateExpenseAsync` akışının (başlıksız) v1.1 idempotency sözleşmesiyle uyumu açısından **kaynak-düzeyi uyumsuzluk/risk** işaretidir; çalışma zamanı davranışı bu denetimde uçtan uca çalıştırılarak doğrulanmadı.

## 4. Altyapı bileşenleri — doğrulanmış davranış ve entegrasyon durumu

### 4.1 `AddExpenseViewModel` (`src/Mobile/FinTrack.Mobile.Core/ViewModels/AddExpenseViewModel.cs`)
- Form yükleme GET çağrıları yapar (`:43-78`).
- Kayıt akışı tek adımlı ve doğrudan: validate → busy → `CreateExpenseAsync` → navigate (`:80-106`).
- Yeniden deneme koruması, "aynı isteği tekrar gönderme engeli", kullanıcıya `Created/SentUnconfirmed/Uncertain` durumu gösterimi, pending kayıt yaşam döngüsü: **yok**.
- `IsBusy` UI kilidi olabilir (`ViewModelBase` davranışı bu dosyada tanımlı değil, doğrulanmadı); ancak bu, kalıcı idempotency/pending koruması değildir.

### 4.2 `FinTrackApiClient` (`src/Mobile/FinTrack.Mobile.Core/Api/FinTrackApiClient.cs`)
- Tüm POST/PUT'lar `JsonContent.Create` ile üretilir (`:121-137`), header eklenmez.
- `Idempotency-Key` üretimi/korunması/saklanması: **yok**.
- `PendingOperationStore`/`PendingOperationRecoveryService` bağımlılığı: **yok**.

### 4.3 `PendingOperationStore` (`src/Mobile/FinTrack.Mobile.Core/PendingOperations/PendingOperationStore.cs`, arayüz `IPendingOperationStore.cs`)
- **Anahtar:** `AddAsync` `Guid.NewGuid().ToString("D")` üretir ve çakışma kontrolü yapar (`:94-99`); kullanıcı UUID üretmez.
- **Şifreleme/gövde:** AES-256-GCM, her yazmada yeni nonce, AAD `Key/Method/Route`, 64 KB sınırı (`:21-23`, `:66-68`, `:349-365`, `:491-505`).
- **Kapasite/eviction:** 50 kayıt; en eski silinir ve `OperationEvicted` olayı yükseltilir (`:45`, `:86-93`, `:506-516`).
- **Okuma/bozulma:** `TryReadBodyAsync` anahtar yok/bozuk, nonce/tag uzunluğu hatalı veya `CryptographicException` (AAD/tag/gövde uyumsuzluğu) durumunda `null` döner; kayıt silinmez (`:148-191`).
- **Durum API'leri:** `SetStateAsync`, `IncrementAttemptAsync`, `MarkTombstoneCheckedAsync`, `DeleteAsync`, `RewriteBodyAsync` mevcut (`:219-315`).
- **Gerçek akış kullanımı:** yalnızca DI kaydı (`MauiProgram.cs:33-37`) ve testlerde; ViewModel/client akışında kullanılmıyor.

### 4.4 `PendingOperationRecoveryService` (`src/Mobile/FinTrack.Mobile.Core/PendingOperations/PendingOperationRecoveryService.cs`)
- **Durum makinesi:** `CreateAsync` kaydı `Created` olarak oluşturur (`:47-54`, store). `Created` otomatik gönderilmez (`:79-95` ve yorum `:10-11`). `SendAsync` `Created` ise `SentUnconfirmed`'a çeker sonra `ExecuteAsync` çalıştırır (`:70-76`).
- **Aynı key/body replay:** `SendPostAsync` aynı `operation.Key`'i `Idempotency-Key` olarak ekler ve saklanan şifreli gövdeden çözülen aynı baytları gönderir (`:204-218`).
- **Recover:** sadece `SentUnconfirmed` listelenir/kurtarılır (`:80-95`, `:97-108`).
- **Denemeler/backoff:** `MaxAttempts=3`, 500ms/1s/2s backoff (`:15`, `:22-23`, `:136-194`, `:304-308`).
- **24h/30h:** `<24h` tam gövdeyle execute; `>=24h && <30h` status kontrolü (`idempotency/{key}` GET) (`:24-25`, `:128-131`, `:221-279`); `>=30h` uncertain (`:123-126`).
- **Belirsizlik:** gövde çözülemezse, 30h aşımı, status `NotFound`/Completed-InProgress dışı/beklenmeyen JSON vb. durumlarda `Uncertain` işaretlenir ve kayıt silinmez (`:114-119`, `:249-277`, `:281-291`).
- **Definitive failure:** 400 veya 409+`IdempotencyKeyReused` → silinir (`:148-155`, `:293-302`). `IdempotencyExpired` kodunda status kontrolüne düşer (`:163-166`).
- **401:** pending kalır, oturum yenilenince devam mesajı (`:171-177`, `:257-263`).
- **Gerçek akış kullanımı:** DI kaydı var (`MauiProgram.cs:58-65`), ancak kaynakta `RecoverAsync/SendAsync/CreateAsync` çağrısı bulunamadı (testler dışında). Başlangıçta tetikleme yok (`App.xaml.cs:23-31`).

### 4.5 `AuthHandler` (`src/Mobile/FinTrack.Mobile.Core/Api/AuthHandler.cs`)
- Bearer ekler, 401'de single-flight refresh dener (`:58-79`).
- POST (auth dışı) için istekten `Idempotency-Key` okunur ve content baytları/bazı header'lar ilk gönderimden önce tamponlanır (`:42-55`).
- Retry koşulu: refresh başarılı **ve** `IsRetryableAsync` true ise; GET/HEAD/OPTIONS her zaman retry edilebilir (`:143-148`), POST için yalnızca `Idempotency-Key` var + store var + auth değil + kayıt durumu `SentUnconfirmed` ise (`:150-159`).
- PUT ve auth POST'ları otomatik retry edilmez (`:150-155`, auth kontrolü `:118-123`). 401 sonrası token yenilense bile güvenli olmayan/retry edilemeyen istek tekrar gönderilmez (`:80-84`).
- `FinTrackApiClient` ve `PendingOperationRecoveryService`'in HttpClient'ları bu handler üzerinden kurulur (`MauiProgram.cs:45-65`) — handler altyapısı gerçek HTTP pipeline'da mevcut, ancak retry'ı tetikleyecek `Idempotency-Key` + `SentUnconfirmed` kayıt kombinasyonu normal akışta üretilmez.

### 4.6 PendingOperation modelleri ve anahtar adlandırması
- Durumlar: `Created`, `SentUnconfirmed`, `Uncertain` (`PendingOperationModels.cs:3-9`).
- Kayıt: key, userIdKey, method, route, şifreli gövde/nonce/tag, zaman, state, attempt, hata, tombstone işareti (`:14-26`).
- Eviction ve gövde sınırı olay/istisnası (`:29-50`).
- SecureStorage anahtar adı: `PendingOperationKeyNames.ForUser` (`src/Mobile/FinTrack.Mobile.Core/Abstractions/ISecureKeyValueStore.cs:21+`, kullanımlar `PendingOperationStore.cs:166`, `:466`).

### 4.7 `MauiProgram` DI ve uygulama başlatma
- Store, `AuthHandler` (store ile), `FinTrackApiClient` (AuthHandler ile), `PendingOperationRecoveryService` (AuthHandler ile) ve `SessionService` kayıtlı (`MauiProgram.cs:30-67`).
- `PendingOperationRecoveryService` herhangi bir ekrana/servise enjekte edilmez; `App.OnStart` sadece `IsAuthenticatedAsync` + navigate yapar (`MauiProgram.cs:69-97`, `App.xaml.cs:23-31`). Recovery başlatma çağrısı yok (doğrulanmış).

### 4.8 İlgili mobil testler (kapsam)
- `tests/FinTrack.Mobile.Tests/PendingOperationStoreTests.cs`: AES-GCM roundtrip/yalnız şifreli saklama, anahtar yoksa gövde okunamaz, kullanıcı anahtarı izolasyonu, AAD bozulması, 64 KB sınırı, 50 kayıt eviction olayı, state filtreleme (doğrulanmış test kapsamı).
- `tests/FinTrack.Mobile.Tests/PendingOperationRecoveryTests.cs`: restart'ta aynı key/body replay (SentUnconfirmed), Created otomatik gönderilmez, definitif 400 silme, `>24h` status kontrolü, decrypt/AAD başarısızlığında `Uncertain` ve silinmeme, bounded retry (3 deneme) — **recovery servisi doğrudan kullanılarak**.
- `tests/FinTrack.Mobile.Tests/AuthHandlerPendingOperationTests.cs`: `SentUnconfirmed` POST'un 401 sonrası aynı key/body ile tekrar edilmesi, POST tekrar edilemeyecek durumlar, GET retry — **store'a elle `SentUnconfirmed` yazılarak ve isteğe header eklenerek**.
- **Eksik entegrasyon kanıtı:** Bu testler gerçek `AddExpenseViewModel`/`FinTrackApiClient` akışının pending-op oluşturduğunu veya recovery'nin uygulama başlangıcında çalıştığını doğrulamaz; kaynak kod bu entegrasyonu göstermemektedir.

## 5. İstenen senaryo analizi

| # | Senaryo | Kodun gerçek davranışı (mevcut akış) | İlgili dosya ve satırlar | Doğrulanmış sonuç | Olası risk | Eksik test | Önerilen çözüm |
|---|---|---|---|---|---|---|---|
| 1 | POST gönderilmeden önce uygulama kapanır | `AddExpenseViewModel.SaveAsync` pending kayıt oluşturmadan doğrudan API çağrısı yapar; kapanma, `await _api.CreateExpenseAsync(...)` öncesinde/istek hazırlanırken olursa ağ çağrısı hiç çıkmayabilir. Kalıcı bir "gönderilecek işlem" kaydı oluşturulmadığı için recovery'nin okuyacağı veri yoktur. | `AddExpenseViewModel.cs:80-106` (`:91` doğrudan gönderim), `MauiProgram.cs:33-65` (altyapı var ama kullanılmıyor), `App.xaml.cs:23-31` (recovery yok) | **Doğrulandı:** kullanıcı niyeti/istek kalıcı pending-op olarak kaydedilmez; uygulama yeniden açıldığında bu işlem otomatik temsil edilmez. (Altyapı tasarımında `Created` "ağa çıkmaz"dır: `PendingOperationRecoveryService.cs:10-11,47-54,70-76`; ancak gerçek akış `CreateAsync` çağırmaz.) | Kullanıcı işlemin kaydedildiğini sanıp sanmama belirsizliği; yeniden denemede manuel tekrar gerekebilir, ancak kayıt hiç oluşmadıysa veri kaybı (kullanıcı aksiyonu kaybolması) yaşanır. | ViewModel bazında "save öncesi/başında pending Created kaydı oluşur" ve "restart sonrası Created otomatik gönderilmez" entegrasyon testi. | Finansal create akışını `RecoveryService.CreateAsync` + kullanıcı aksiyonuna bağlı `SendAsync` (veya eşdeğer akış) ile sarmala; UI'da taslak/pending durumunu göster. |
| 2 | POST gönderildikten sonra cevap gelmeden uygulama kapanır | Gerçek akışta istek gönderimi sırasında kalıcı `SentUnconfirmed` kaydı yoktur. Uygulama kapanırsa istemci yanıtı işleyemez; `AddExpenseViewModel` hata/navigasyon durumunu güncelleyemez. Altyapı olsaydı `SendAsync` gönderim öncesi `SentUnconfirmed` yazar ve aynı key/body saklanırdı (`PendingOperationRecoveryService.cs:70-76,204-218`), recovery de bunu kurtarırdı (`:80-95`). | `AddExpenseViewModel.cs:80-106`, `FinTrackApiClient.cs:77-78,121-128,139-151`, `PendingOperationRecoveryService.cs:70-76,80-95,204-218`, `MauiProgram.cs:58-65`, `App.xaml.cs:23-31` | **Doğrulandı:** gerçek akışta `SentUnconfirmed` kaydı ve yeniden başlatmada otomatik recovery yok. Sunucu tarafı sonucu bu incelemede çalışma zamanı olarak gözlemlenmedi. | Sunucu işlemi işlemiş olsa bile istemci sonucu bilemez; kullanıcı tekrar kaydederse (ayrı, anahtarsız istek) çift kayıt riski oluşabilir. (Not: sunucu idempotency anahtarı gerektirir; anahtarsız in-scope POST reddi middleware tarafından tanımlı — bkz. §3.) | "İstek yollandı ama yanıt yok" durumunda aynı idempotency anahtarlı pending kaydın kaldığı ve restart recovery'nin aynı key/body ile tekrar gönderdiği uçtan uca VM+HTTP testi. | Gönderim öncesi pending-op oluştur ve `SentUnconfirmed` durumunu kalıcı yaz; uygulama açılışında kullanıcı bağlamı için `RecoverAsync` çalıştır; aynı işlemi temsil eden tek anahtarı UI'da koru. |
| 3 | Sunucu kaydı oluşturur ancak mobil istemci cevabı alamaz | Gerçek akış: başarı yanıtı alınamadığı için `AddExpenseViewModel` `//expenses`'e gitmez, catch blokları hata mesajı gösterebilir veya süreç sonlanabilir (`AddExpenseViewModel.cs:91-105`). Kalıcı işlem kimliği/anahtarı tutulmadığından aynı mantıksal işlemi "aynı anahtarla sorgula/tekrarla" mekanizması yok. | `AddExpenseViewModel.cs:80-106`, `FinTrackApiClient.cs:139-151`, karşılaştırma: `PendingOperationRecoveryService.cs:141-146,221-279` (altyapıda başarı/status ile tamamlama) | **Doğrulandı:** mevcut UI/akış, sunucu-commit + istemci-yanıtsız durumunu aynı anahtarla doğrulayamaz ve otomatik uzlaştıramaz. | Mükerrer gönderim, kullanıcıda "başarısız oldu ama kayıt oluştu" algısı, veri tutarsızlığı riski. | Kısmi başarı/yanıt kaybı simülasyonunda tek finansal kayıt ve UI uzlaşması testi. | `Create+Send` tek anahtar modeli; yanıt belirsizse status endpoint (`GET idempotency/{key}`) ile uzlaştırma ve kullanıcıya "sonucu kontrol et / bekle" durumu gösterimi. |
| 4 | Sunucu 401 döndürür ve token yenilenir | `AuthHandler` 401'de refresh dener (`:61-79`). **GET/HEAD/OPTIONS** refresh sonrası yeniden gönderilir (`:143-148`). **POST** sadece `Idempotency-Key` mevcutsa ve pending-op durumu `SentUnconfirmed` ise yeniden gönderilir (`:150-159`); body ilk gönderimden önce tamponlanır (`:42-55,86-95`). **PUT ve auth POST** retry edilmez (`:118-123,150-155`). Gerçek `CreateExpenseAsync` ve `UpdateProfileAsync` çağrıları `Idempotency-Key` üretmediği/taşımadığı için POST retry koşulu oluşmaz; PUT zaten retry edilmez. | `AuthHandler.cs:35-96,118-160`, `FinTrackApiClient.cs:46-53,77-78,121-137`, `MauiProgram.cs:45-57` | **Doğrulandı:** altyapı düzeyinde "SentUnconfirmed + aynı key/body" POST retry kuralı var; gerçek `AddExpense`/profil güncelleme akışlarında bu koşullar sağlanmadığından otomatik güvenli yeniden gönderim devreye girmez. | 401 sonrası kullanıcı işlemi yarıda kalır; manuel tekrar gerekebilir, manuel tekrar ise idempotency anahtarı olmadan mükerrer kayıt riski taşıyabilir. | ViewModel akışında 401+refresh sonrası aynı idempotency anahtarıyla expense POST'unun yeniden denendiği entegrasyon testi. | Finansal POST'ları pending-op (`SentUnconfirmed`) + `Idempotency-Key` ile gönder; profil gibi PUT akışları için de açık retry/çakışma stratejisi tanımla (idempotency kapsamı ürün kararı gerektirir). |
| 5 | Kullanıcı aynı ekrana dönerek tekrar Kaydet'e basar | `AddExpenseViewModel.SaveAsync` her çağrıda mevcut form değerleriyle yeni bir `CreateExpenseRequest` üretir ve yeni bir `CreateExpenseAsync` çağrısı yapar (`:112-160`, `:80-106`). İstemci tarafında aynı işlemi tanımlayan kalıcı anahtar, "devam eden/gönderildi-belirsiz" kontrolü veya tekrar basışı engelleme yoktur (yalnızca genel `IsBusy` durumu ViewModelBase'de olabilir; doğrulanmadı). | `AddExpenseViewModel.cs:80-106,112-160`, `FinTrackApiClient.cs:77-78` | **Doğrulandı:** aynı kullanıcı aksiyonunun tekrarında aynı idempotency anahtarının korunması/yeniden kullanılması yok; ikinci bir HTTP POST tetiklenebilir. | Çift harcama kaydı veya mükerrer işlem riski (özellikle ağ gecikmesi/timeout/401 sonrası). | "Kaydet" çift tıklama / geri dönüp tekrar kaydetme senaryosunda tek pending-op ve tek sunucu etkisi testi. | İşlem bazlı idempotency anahtarını UI/VM yaşam döngüsünde sabitle; in-flight veya `SentUnconfirmed/Uncertain` varken yeni kayıt yerine mevcut işlemin durumunu göster/uzlaştır. |
| 6 | Uygulama 24 saatten uzun süre kapalı kalır | Altyapı (`PendingOperationRecoveryService`) gerçek akışta çağrılmadığından bu zaman aşımı mantığı çalışmaz. **Altyapı davranışı (doğrulanmış, servis doğrudan kullanılırsa):** `FullBodyTtl=24h` sonrası gövde yerine status kontrolü yapılır (`:24,128-131,221-279`), `TombstoneTtl=30h` sonrası `Uncertain` işaretlenir (`:25,123-126`), tombstone/status sonucu `NotFound` vb. durumda `Uncertain` ve silinmez (`:252-255,281-291`). | `PendingOperationRecoveryService.cs:22-25,110-131,221-279,281-291`, `MauiProgram.cs:58-65`, `App.xaml.cs:23-31` | **Doğrulandı:** gerçek akışta 24h+ recovery uygulanmaz. Altyapı kodu 24h/30h dallarını içerir ancak üretim akışına bağlı değildir. | Eski belirsiz işlemler sessizce kalabilir (kayıt hiç oluşmadıysa) veya altyapı entegre edilirse yanlış entegrasyonda durumlar kullanıcıya görünmeyebilir. | 24h/30h sınırında recovery + status endpoint + UI mesajı entegrasyon testi (gerçek çağıran bileşen üzerinden). | Uygulama açılışında/uygun noktada recovery çalıştır; 24h sonrası status-check ve `Uncertain` sonuçlarını kullanıcıya açıkça raporla, yeni UUID üretme. |
| 7 | PendingOperation şifre çözme başarısız olur veya SecureStorage anahtarı kaybolur | **Altyapı davranışı (doğrulanmış):** `PendingOperationStore.TryReadBodyAsync` anahtar bulunamaz/okunamaz/uzunluk hatalı veya decrypt/AAD hatasında `null` döner (`:148-191`); kayıt silinmez. `PendingOperationRecoveryService.ExecuteAsync` body `null` ise `MarkUncertainAsync` ile `Uncertain` yapar ve `UnknownResultMessage` üretir; yeni UUID üretmez (`:114-119,281-291`). **Gerçek akış etkisi:** gerçek mutasyonlar pending-op oluşturmadığı için bu durum pratikte bu akışlardan kaynaklanan kayıtlar için oluşmaz. | `PendingOperationStore.cs:148-191,466-489`, `PendingOperationRecoveryService.cs:114-119,281-291,16-18`, `ISecureKeyValueStore.cs:21+` | **Doğrulandı (altyapı):** decrypt/anahtar kaybı durumunda kayıt korunur ve `Uncertain`'a çekilir. **Doğrulandı (gerçek akış):** gerçek write akışları bu depoyu kullanmadığından bu koruma onlara uygulanmıyor. | Anahtar kaybı sonrası belirsiz işlemin sonucu kullanıcı tarafından manuel kontrol edilmelidir; entegrasyon yoksa zaten işlem takibi yoktur. | Anahtar silinmesi/bozulması sonrası VM/UI'nın "sonuç bilinmiyor" durumunu gösterdiği entegrasyon testi. | Mevcut store/recovery belirsizlik davranışını koruyarak finansal akışları bu depoya bağla; kullanıcıya kayıtları kontrol ettiren net durum ekranı ekle. |

## 6. Finansal mutasyon akışları — nihai tablo

Not: "Finansal mutasyon" burada sunucu tarafında finansal kayıt oluşturan/değiştiren etkiyi ifade eder. Mobil kaynakta bulunan tüm POST/PUT/mutasyon çağrıları kapsama için listelenmiş; finansal olmayanlar ayrıca işaretlenmiştir.

| Akış | Tetikleyen UI/VM | Client metodu | HTTP / yol | Finansal? | Sunucu idempotency kapsamında mı? (kaynak) | Idempotency-Key üretiliyor/korunuyor mu? | Aynı body korunuyor mu? | PendingOperation kaydı var mı? | `SentUnconfirmed`/`Uncertain` yönetimi (gerçek akış) | Recovery bu akışı geri kazanabiliyor mu? | Tekrar Kaydet'te ikinci işlem engeli | Genel değerlendirme |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Harcama oluşturma | `AddExpenseViewModel.SaveAsync` (`AddExpenseViewModel.cs:80-106`) | `CreateExpenseAsync` (`FinTrackApiClient.cs:77-78`) | `POST expenses` | **Evet** | Evet, in-scope prefix (`IdempotencyMiddleware.cs:20-28,390-396`) | Hayır (`FinTrackApiClient.cs:121-128,139-151`) | Hayır (her çağrı yeni request/istek) | Hayır | Hayır | Hayır | Hayır | **Entegre değil / riskli** |
| Profil güncelleme (saat dilimi) | `SettingsViewModel.SaveAsync` (`SettingsViewModel.cs:56-92`) | `UpdateProfileAsync` (`FinTrackApiClient.cs:46-53`) | `PUT auth/me` | Hayır (profil) | Hayır, middleware POST-only ve auth hariç (`IdempotencyMiddleware.cs:385-396`) | Hayır | Hayır | Hayır | Hayır | Hayır | Hayır (409 için kullanıcıya yenileme mesajı var, `:78-83`) | Pending/idempotency entegrasyonu yok; concurrency 409 ile yüzeyleniyor |
| Çıkış | `SettingsViewModel` → `SessionService.LogoutAsync` (`SettingsViewModel.cs:95-107`, `SessionService.cs:39-59`) | `LogoutAsync` (`FinTrackApiClient.cs:33-41`) | `POST auth/logout` | Hayır | Hayır (auth hariç) | Hayır | Hayır | Hayır | n/a | Hayır | n/a | Idempotency/pending beklenmez (auth dışı) |
| Giriş | `LoginViewModel` → `SessionService.LoginAsync` | `LoginAsync` (`FinTrackApiClient.cs:20-21`) | `POST auth/login` | Hayır | Hayır (auth hariç) | Hayır | Hayır | Hayır | n/a | Hayır | n/a | Idempotency/pending beklenmez (auth dışı) |
| Kayıt | `RegisterViewModel` → `SessionService.RegisterAsync` | `RegisterAsync` (`FinTrackApiClient.cs:23-31`) | `POST auth/register` | Hayır | Hayır (auth hariç) | Hayır | Hayır | Hayır | n/a | Hayır | n/a | Idempotency/pending beklenmez (auth dışı) |

**Ek doğrulama:** `src/Mobile` altında `PendingOperationRecoveryService.CreateAsync/SendAsync/RecoverAsync` çağrısı bulunmadı; `IPendingOperationStore` yalnızca DI ve altyapı sınıfları içinde kullanıldı (uygulama/VM akışında değil). Bu, yukarıdaki "gerçek akış" sonuçlarını destekler.

## 7. Sonuç ve önerilen çözüm öncelikleri (raporlama amaçlı)

1. **Kritik (finansal akış):** `AddExpenseViewModel` + `FinTrackApiClient.CreateExpenseAsync` akışını mevcut `PendingOperationRecoveryService`/store ile entegre et; işlem başına tek idempotency anahtarı üret/sakla/yeniden kullan, aynı gövde baytlarını koru, `Created/SentUnconfirmed/Uncertain` durumlarını kullanıcıya görünür kıl.
2. **Kritik (yaşam döngüsü):** Uygulama açılışında veya oturum hazır olduğunda ilgili kullanıcı için `RecoverAsync` tetikle; sonuçları (`Completed/Uncertain/Pending`) UI/telemetri ile yüzeyle.
3. **Davranış netleştirme:** `AuthHandler`'ın 401+retry korumasından yararlanmak için isteklerin `Idempotency-Key` taşıması ve store'da `SentUnconfirmed` kaydının bulunması gerekir; bunu gerçek gönderim akışıyla hizala.
4. **Test boşlukları:** Mevcut testler altyapıyı doğruluyor; eksik olan ViewModel/typed-client entegrasyon testleri (save → pending → send/restart/recover/çift tıklama/401/24h+/decrypt-fail) eklenmeli.
5. **Sözleşme uyumu:** `/api/v1/expenses` için sunucu `Idempotency-Key` zorunlu kıldığı için (`IdempotencyMiddleware.cs:60-68`), mobil create akışı bu gereksinimle uyumlu hale getirilmeli (uygulama/çalışma zamanı doğrulaması ayrıca yapılmalıdır).

*Bu rapor yalnızca kaynak kod ve mevcut test dosyaları üzerinden doğrulanabilen bulguları içerir; manuel UI smoke, canlı API çağrısı veya tam entegrasyon koşumu bu denetimde yapılmamıştır.*

> **Okuma notu (2026-11-20):** §1–§7, V11-T02 öncesindeki kaynak durumu
> gösteren **tarihsel denetim anlık görüntüsüdür**. Satır referansları ve
> "entegre değil" yargıları o an için doğrudur; aşağıdaki §8 güncel kapanış
> durumunu geçersiz kılar.

---

## 8. V11-T02 sonrası kapanış durumu (2026-11-20)

### 8.1 Y-1 / P-1 kapsamı

| Bulgu | Kapanan kısım | Açık kalan kısım |
|---|---|---|
| **Y-1 — gerçek yazma akışı ile pending/recovery altyapısı arasındaki boşluk** (`architecture-audit.md` G-5, orta/teknik borç satırları) | `AddExpenseViewModel.SaveAsync` artık doğrudan `CreateExpenseAsync` çağırmıyor; `FindMatchingAsync`/`CreateAsync` → `SendAsync`/`RecoverOneAsync` kullanıyor. Gövde bir kez serileştiriliyor; aynı UUID `D` anahtarı ve aynı baytlar pending kaydına ve HTTP POST'a veriliyor. `SentUnconfirmed` gönderimden önce kalıcı, 2xx'te kayıt siliniyor, belirsizlikte korunuyor. 14 yeni test bu zinciri doğruluyor. | Kart/borç/ödeme/para girişi mutasyonları ve profil `PUT` hâlâ entegre değil; bu görev kapsam dışıydı. |
| **P-1 — mobil mutasyon POST'larının tam entegrasyonu (zorunlu)** (`architecture-audit.md` P-1) | Kritik finansal akış olan harcama oluşturma için kapatıldı: `Idempotency-Key` doğru istekte, `AuthHandler` koşullu POST retry `SentUnconfirmed` ile çalışıyor, 401+refresh sonrası aynı key/body yeniden gönderiliyor, 24 s+ durum sorgusu ve `Uncertain` davranışı korundu. | "Tüm mutasyon POST" hedefi kapsam dışı akışlar için açık; login/register/logout auth kapsam dışı olduğundan bilinçli olarak değiştirilmedi. |
| **Başlangıçta recovery çağrısı yok** (bu rapor §1, §4.4, §4.7) | `App.OnStart` navigasyondan sonra `PendingOperationStartup.RecoverCurrentUserAsync`'u çağırıyor. Oturum/JWT `sub` hazır olmadan recovery yapılmıyor; `SemaphoreSlim(1,1)` single-flight, hata yutuluyor, UI bloklanmıyor. | Kurtarma sonuçları startup'ta doğrudan UI'ya yansıtılmıyor; kayıtlar/`LastMessage` üzerinden izlenir. Açık kullanıcı arayüzü tasarımı bu görev kapsam dışıydı. |
| **Test boşluğu — ViewModel → PendingOperation → HTTP zinciri** (bu rapor §4.8, §7.4) | `ExpensePendingOperationIntegrationTests` içinde 14 test; görev kartı §4 senaryoları 1–10, oturum değişimi, kullanıcı izolasyonu, JWT `sub` yokluğu ve 31 gün tombstone kapsandı. Mobil paket **45/45**, Docker hariç çözüm testleri **354/354**, Android + Windows build **0/0**. | API entegrasyonları bu oturumda Docker daemon kapalı olduğu için yeniden çalıştırılamadı (86/95 `DockerUnavailableException`; 9 DB'siz smoke geçti); Windows manuel UI smoke operatör tarafından henüz yapılmadı. |

### 8.2 Korunan sınırlar ve bilinçli sapmalar

- Modular Monolith, modül sınırları, host middleware şeması, AES-256-GCM +
  AAD, kullanıcı izolasyonu ve `Created`/`SentUnconfirmed`/`Uncertain`
  anlamları değişmedi. Yeni paket, tablo, migration, CQRS veya state machine
  eklenmedi.
- `FinTrackApiClient.CreateExpenseAsync` finansal POST için kullanılmıyor;
  typed client form GET'leri için korundu. Gerekçe: pending durum geçişi ve
  bayt-identik replay yalnızca recovery servisinin gönderim yoluyla
  garanti edilebiliyor (bkz. `docs/11` §9.6).
- Yerel `UserIdKey`, auth akışı değiştirilmeden JWT `sub`'dan türetiliyor;
  oturum değişirse pending kayıt korunuyor ve başka kullanıcının token'ıyla
  gönderilmiyor.
- Görev kartındaki "30 saat" ifadesi V11-T01'in 30 günlük tombstone
  sözleşmesiyle çeliştiği için kaynak sözleşme korundu; 31. saat ve 31. gün
  ayrı testlerle doğrulandı.

**Sonuç:** Bu denetimin **Y-1 ve P-1 bulguları harcama oluşturma akışı için
kapatıldı**; kalan maddeler kapsam dışı mutasyonlar, operasyonel Docker/CI
doğrulaması ve Windows manuel UI smoke'tur. Kaynak kod ve test sonuçları
`docs/08` §8.7, `docs/09` v1.25 ve `docs/11` v1.4/§9.6 ile tutarlıdır.
