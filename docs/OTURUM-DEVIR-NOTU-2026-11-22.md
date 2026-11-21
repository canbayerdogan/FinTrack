# FinTrack — Oturum Devir Notu (2026-11-22)

> Bu not, 2026-11-22 oturumu kapanırken bir sonraki oturumun **kaldığı yerden
> devam edebilmesi** için hazırlandı. Kod değişiklikleri **commit `f039897`**
> (`feat(mobile): add idempotent card creation screen`) ile `origin/main`'e
> push edildi. Bu devir notu ayrı bir docs commit'i olarak push edilir; kendi
> commit hash'i için `git log -2 --oneline` kullanılabilir. Çelişki halinde bu
> not esas alınır.

---

## 1. Genel durum

- Depo: `C:\Users\Erdogan\source\FinTrack`, dal: `main`.
- Kod tarafı commit/push edildi; çalışma ağacı bu not dışında temiz.
- Bu oturumun iki ana işi:
  1. Android emulator'da **fiziksel klavye** girişini düzeltmek.
  2. Mobil **Kart ekle** ekranını eklemek ve uçtan uca doğrulamak.
- **Bloker yok.** Sonraki oturum doğrudan yeni bir işle başlayabilir.

---

## 2. Bu oturumda yapılanlar

### 2.1 Fiziksel klavye (AVD, repo dışı)

- `C:\Users\Erdogan\.android\avd\fintrack_smoke.avd\config.ini` içinde
  `hw.keyboard=no` idi; `hw.keyboard=yes` yapıldı.
- Emulator `-no-snapshot-load` ile yeniden başlatıldı. Android artık
  `AT Translated Set 2 keyboard` bildiriyor.
- `Harcama ekle` tutar alanında hem F-row rakamları hem sayısal tuş takımı
  çalışıyor. `InputValidation.TryParseAmount` zaten `,` ve `.` ayırıcısını
  kabul ediyordu; üretim kodu değiştirilmedi.
- Emulator: `fintrack_smoke` / `emulator-5554`.

### 2.2 Mobil kart ekleme

- `Kartlar → Ekle` rotası ve ekranı eklendi (`addcard`).
- Zorunlu alanlar: **Banka adı**, **Kart adı**.
- Opsiyonel alanlar: kredi limiti, ekstre günü (1–31), son ödeme günü (1–31).
- **Kart numarası, CVV veya son kullanma tarihi istenmiyor / gönderilmiyor.**
- `POST /cards`, `IdempotencyMiddleware.InScopePrefixes` içinde olduğu için
  kayıt, `Harcama ekle` ile aynı güvenli yoldan yapılıyor:
  `PendingOperationRecoveryService` + `SessionService`, UUID `Idempotency-Key`,
  pending kaydı ve tüm denemeler için aynı gövde baytları.
- Anahtarsız direkt `CreateCardAsync` çağrısı **kaldırıldı**; `CreateCardRequest`
  DTO'su kaldı.

**Yeni dosyalar**
- `src/Mobile/FinTrack.Mobile.Core/ViewModels/AddCardViewModel.cs`
- `src/Mobile/FinTrack.Mobile/Views/AddCardPage.xaml`
- `src/Mobile/FinTrack.Mobile/Views/AddCardPage.xaml.cs`
- `tests/FinTrack.Mobile.Tests/CardPendingOperationIntegrationTests.cs`

**Değiştirilen dosyalar**
- `src/Mobile/FinTrack.Mobile.Core/Api/FinTrackApiClient.cs`
- `src/Mobile/FinTrack.Mobile.Core/ViewModels/CardsViewModel.cs`
- `src/Mobile/FinTrack.Mobile/AppShell.xaml.cs`
- `src/Mobile/FinTrack.Mobile/MauiProgram.cs`
- `src/Mobile/FinTrack.Mobile/Views/CardsPage.xaml`
- `tests/FinTrack.Mobile.Tests/ViewModelTests.cs`
- `README.md`
- `docs/07-yol-haritasi-ve-ajan-gorev-standardi.md`

### 2.3 Emulator uçtan uca sonucu

Temiz kayıt sonrası ekran otomatik olarak `Kartlar`'a döndü:

```text
Kartlar
2 aktif kart
Ekle / Yenile
Hesap karti — Is bankasi — Limit yok — Active
Axess karti — Akbank — 50.000,50 TRY — Active
```

API log'u: `POST /api/v1/cards → 201`, ardından `GET /api/v1/cards → 200`.

---

## 3. Ortam / smoke notları (repo dışı)

- Cihaz uygulama tercihi: `http://10.0.2.2:5241/api/v1/`.
  Repo varsayılanı HTTPS-only (`https://10.0.2.2:7241/api/v1/`); dev sertifikanın
  SAN'ında `10.0.2.2` yok ve system CA kurulumu
  (`Device must be bootloader unlocked`, `adb remount` başarısız) engelli.
- API her zaman şu komutla başlatılmalı:

  ```powershell
  dotnet run --project src/Host/FinTrack.Api --launch-profile https
  ```

  `--launch-profile https` olmadan Development JWT signing-key hatası oluşur.
  Her yeniden başlatmada JWT signing key değişir; login token'ları yenilenmelidir.
- Smoke kullanıcısı: `smoke_20261122@example.com` / `GucluParola1`.
- Yerel MSSQL/LocalDB'de eksik olan `shared.IdempotencyKeys` tablosu için
  migration uygulandı (kalıcı):

  ```powershell
  dotnet run --project src/Host/FinTrack.Api --launch-profile https -- --migrate
  ```

- Smoke APK kurulumu için manifeste geçici
  `android:usesCleartextTraffic="true"` eklendi, kurulumdan hemen sonra **geri
  alındı**. Repo manifesti temiz. Yeniden kurulum gerekirse geçici olarak açıp
  kurulumdan sonra tekrar kaldır:

  ```powershell
  dotnet build src/Mobile/FinTrack.Mobile/FinTrack.Mobile.csproj -f net10.0-android -t:Install --no-incremental -p:JavaSdkDirectory="C:\Program Files\Eclipse Adoptium\jdk-17.0.21.9-hotspot" --nologo -v q
  ```

- `pm clear com.fintrack.app` fast-deployment assembly'lerini siler; gerekirse
  yukarıdaki `-t:Install` komutuyla yeniden kurulmalı.
- Migration öncesi başarısız denemeden kalan `SentUnconfirmed / HTTP 500`
  pending kaydı emulator veritabanından temizlendi; cihazda bekleyen işlem
  sayısı **0**.
- API bu oturumda arka plan shell'i olarak açıktı; oturum kapanınca süreç
  kapanabilir. Gerekirse yukarıdaki komutla yeniden başlat.
- `adb` bazen daemon yeniden başlatınca cihazı `offline` gösteriyor;
  `adb wait-for-device` veya emulator restart yeterli.

---

## 4. Doğrulama (hepsi yeşil)

| Komut / kontrol | Sonuç |
|---|---|
| `dotnet build FinTrack.slnx --no-restore --nologo -v q` | **0 uyarı / 0 hata** |
| Docker hariç 8 test projesi | **395/395** |
| └ `FinTrack.Mobile.Tests` | **50/50** |
| `dotnet test tests/FinTrack.Api.IntegrationTests --nologo -v q` (Docker + MSSQL) | **95/95** |
| F-06 özel testi (`Enlist_istisnasi_mevcut_politikada_yayilir_ve_hicbir_kayit_yazilmaz`) | **1/1** |
| Android build (`-f net10.0-android`, JDK 17) | **0 uyarı / 0 hata** |
| `git diff --check` | Temiz (yalnızca LF→CRLF uyarıları) |

Dockerless dağılım: Architecture 17, API Unit 88, CreditCards 86, Debts 38,
Expenses 58, CashInflows 48, Reporting 10, Mobile 50.

---

## 5. Git durumu

- `8bee17e` → **önceki HEAD** (V12-T02, zaten push'lu).
- `f039897` → **kod commit'i**, push edildi:
  `feat(mobile): add idempotent card creation screen`.
- Bu notun commit'i: `docs: record 2026-11-22 session handoff` (push edilir).
- Yeni CI run başlatılmadı. Son bilinen yeşil CI eski bir commit'e ait.

---

## 6. Kalanlar / sonraki oturum için notlar

1. **Aksiyon bekleyen bloker yok.** Kart ekleme ve klavye işi tamamlandı.
2. README'deki test sayıları hâlâ V11-T01 dönemine ait (`31 mobil`). Gerçek
   mobil test sayısı artık **50**; toplam Dockerless **395**. İstenirse ayrı bir
   docs commit'iyle güncellenebilir (bu oturumda dokunulmadı).
3. Windows manuel UI smoke yapılmadı; Android smoke yapıldı.
4. Önceki audit'lerden **F-09, F-10, F-14** açık kalmıştı; bu oturumda bunlara
   dokunulmadı.
5. Cihazdaki cleartext ayarı yalnızca geçici kurulum içindi; depoda kalıcı
   cleartext **yok**.
6. `Kartlar` ekranında kart arşivleme/düzenleme UI'ı hâlâ yok; backend uç
   noktaları mevcut. İstenirse sonraki iş olabilir.
