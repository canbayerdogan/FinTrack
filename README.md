# FinTrack

[![CI](https://github.com/canbayerdogan/FinTrack/actions/workflows/ci.yml/badge.svg)](https://github.com/canbayerdogan/FinTrack/actions/workflows/ci.yml)

Kişisel finans ve harcama takibi uygulaması.

**Nihai ürün:** mobil uygulama (.NET MAUI)
**Backend:** ASP.NET Core Web API · SQL Server · .NET 10
**Mimari:** modüler monolit · modül içi Clean Architecture · CQRS (mediator'süz)

> Bu depo şu anda **tasarım + iskelet + kimlik doğrulama + harcama/kategori +
> kredi kartı + ekstre/ödeme + para girişi + nakit avans + kredi/kişisel borç +
> raporlama + MAUI istemci** aşamasındadır. Çözüm iskeleti (slice 0), kimlik
> doğrulama (slice 1), harcama/kategori (slice 2), kart + hesaplanan bakiye
> (slice 3), ekstre + kart ödemesi (slice 4), para girişi (slice 5), nakit avans
> (slice 6), kredi/kişisel borç (slice 7), raporlama (slice 8) ve MAUI istemci
> (slice 9) yazıldı; backend + Android + Windows hedefleri derlenir, testleri
> geçer. Android emülatöründe giriş → harcama → kart → rapor uçtan uca smoke
> tamamlandı. Son doğrulanan CI run'ları `44365407221` (CI bakımı),
> `44366617692` (dokümanlar) ve `44379256250` (Android düzeltmeleri) yeşil.
> Kod yazılmadan önce dokümanların okunması zorunludur.

---

## Ürün tek cümlede

Kullanıcının **kendi girdiği** harcama, para girişi ve borç bilgilerinden parasının nereye gittiğini ve ay sonunda gerçekte ne durumda olduğunu gösteren bir uygulama.

## Ne değiliz

| Değiliz | Sonucu |
|---|---|
| Muhasebe yazılımı | Ana para / faiz / KKDF / BSMV kırılımı tutmayız. Aylık toplam yükümlülük yeterli. |
| Bankacılık uygulaması | Banka API'si yok. Kart numarası, CVV, internet bankacılığı şifresi istemeyiz ve saklamayız. |
| E-ticaret entegrasyonu | Sipariş/ödeme altyapısı yok. |
| Yatırım danışmanlığı | "Şunu al, bunu sat" demeyiz. Haber/bilgi veririz, yönlendirmeyiz. |

## Değişmez ürün kuralları

1. **Harcama, ödeme, para girişi ve borç ayrı kavramlardır.** Tek bir "işlem" tablosuna indirgenemez.
2. **Kredi kartı borcu hesaplanır**, elle tutulmaz. Ekstre dönemsel özettir; yeni gider veya ikinci borç değildir.
3. **Aynı ekonomik olay iki kez gelir/gider sayılmaz.** (Kart borcu ödemesi ≠ ikinci market harcaması)
4. **Nakit avans** ayrı ama kart borcuyla ilişkili bir borçtur; toplam borçta mükerrer sayılmaz.
5. **Para girişi gelir değildir.** Kredi, nakit avans, borç alma ve varlık satışı kazanılmış gelir sayılmaz.
6. **Kaynak türü korunur.** Maaş, kredi, nakit avans, kişiden borç ayrı kaynaklardır.
7. **AI hesaplama otoritesi değildir.** Bakiye ve toplamlar deterministik uygulama mantığıyla hesaplanır.
8. **Kayıt girilmemesi, harcama yapılmadığının kanıtı değildir.** Bildirimler kesin yargı kurmaz.

---

## Dokümanlar

| # | Doküman | Durum |
|---|---|---|
| 00 | [Kaynak konuşma](docs/_source/) — ürün fikrinin çıkış noktası | ✅ |
| 01 | Product Vision & Scope | ✅ (kaynakta mevcut) |
| 02 | [Finansal Alan Modeli — kararlar v0.4](docs/02-finansal-alan-modeli-kararlar.md) | ✅ |
| 03 | [Teknik Mimari (.NET)](docs/03-mimari.md) | ✅ |
| 04 | [ADR'ler](docs/adr/) | ✅ |
| 05 | [İş kuralları (business rules) v1.6](docs/05-is-kurallari.md) | ✅ |
| 06 | [API sözleşmeleri v1.7](docs/06-api-sozlesmeleri.md) | ✅ |
| 07 | [Yol haritası ve ajan görev standardı v1.18](docs/07-yol-haritasi-ve-ajan-gorev-standardi.md) | ✅ |
| 08 | [Durum ve devam notu v2.17](docs/08-durum-ve-devam-notu.md) | ✅ |
| 09 | [Görev listesi (TODO) v1.27](docs/09-todo.md) | ✅ |
| 10 | [Kararlar v1.2](docs/10-karar-bekleyen-konular.md) | ✅ yanıtlandı |
| 11 | [Idempotency teknik tasarımı v1.4](docs/11-idempotency-teknik-tasarim.md) | ✅ koşullu onay + açık kodlama onayı — V11-T01 uygulandı (435/435 test) |
| 12 | [ADR-0010 — host sahipliğinde idempotent yazma](docs/adr/0010-host-owned-idempotent-writes.md) | ✅ koşullu onay |

## Mevcut durum

- **Slice 0 — İskelet:** ✅ 28 proje (slice 5 sonrası), `dotnet build` 0 hata, 0 uyarı.
- **Slice 1 — Kimlik doğrulama:** ✅ ASP.NET Core Identity + JWT, rotating
  refresh token (DB'de hash'li), `identity` şeması/migration, `POST /auth/register`,
  `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET/PUT /auth/me`.
  22 birim + 17 entegrasyon kimlik testi.
- **Slice 2 — Harcama + kategori:** ✅ `expenses` şeması/migration, harcama,
  iade ve kategori uç noktaları (`docs/06` §3–4). Kart alışverişi `CardId` ile
  aynı harcama kaydıdır (ADR-0008). 52 modül + 10 entegrasyon testi (kesit
  testleriyle toplam Expenses 55).
- **Slice 3 — Kart + hesaplanan bakiye:** ✅ `cards` şeması/migration, kart
  CRUD + arşivleme, `GET /cards/{id}/balance` (harcama − iade; ödeme ve nakit
  avans sonraki dilimlerde). Bilinmeyen kart alanları (kart no/CVV/SKT) `400`
  ile reddedilir. 22 modül + 6 entegrasyon testi.
- **Slice 4 — Ekstre + kart ödemesi:** ✅ `cards.CardPayments`/`cards.Statements`
  migration'ı, ödeme CRUD/iptal, ekstre oluşturma/iptal/listeleme. Türetilen
  `carriedOverAmount`, `computedDebtAtPeriodEnd`, `reconciliationDifference`
  okuma anında hesaplanır. Bakiye `payments` terimini içerir;
  `GET /cards/{id}/balance?asOf=YYYY-MM-DD` geçmiş kesit destekler; gelecek
  `asOf` `400` döner. 57 modül + 6 entegrasyon testi.
- **Slice 5 — Para girişi:** ✅ `inflows` şeması/migration, `CashInflow` CRUD ve
  iptal. `isEarnedIncome` kaynaktan türetilir (`Unknown` → `null`); istemci
  gönderirse `400`. `BankLoan`, `CashAdvance`, `PersonalBorrowing` doğrudan
  oluşturulamaz (`400 DebtSourceRequiresOwnerEndpoint`). Borç bağlantılı kayıt
  `debtLink` döner; doğrudan güncelleme/iptal `409`. 48 modül + 5 entegrasyon
  testi; `_status` yalnızca Reporting'de kaldı.
- **Slice 6 — Nakit avans:** ✅ Tamamlandı ve CI'da doğrulandı. `cards.CashAdvances` migration'ı,
  `GET/POST /cards/{id}/cash-advances` ve iptal uç noktası; borç formülünde
  `+ cashAdvances`. ADR-0009 ile avans ve bağlı `CashInflow` aynı DB
  transaction'ında yazılır/iptal edilir; yarı kayıt kalmaz. 21 yeni modül + 5
  yeni API entegrasyon testi eklendi.
- **Slice 7 — Kredi + kişisel borç:** ✅ `debts` şeması/migration, tam taksit
  planı, sıralı ödeme ve iptal, kişisel borç/geri ödeme, kalan borç formülleri
  ve ADR-0009 ile atomik para girişi. `GET/POST /loans` ve
  `GET/POST /personal-debts` uç noktaları; Debts `_status` kaldırıldı.
  31 modül + 8 yeni API entegrasyon testi eklendi.
- **Slice 8 — Raporlama:** ✅ Salt okunur 3 uç nokta: `GET /reports/monthly`,
  `/reports/debt-summary`, `/reports/upcoming-payments`. Veri yalnızca
  `ModuleContracts` üzerinden okunur; Reporting'in DbContext'i/migration'ı yok.
  Takvim ayı, `Europe/Istanbul` varsayılan, kullanıcı tercihi saat dilimi; borç
  formülünde nakit avans bir kez, ekstre eklenmez, kart alacağı ayrı. `_status`
  kaldırıldı, route grubu `RequireAuthorization()`. 10 modül + 7 yeni API
  entegrasyon testi eklendi.
- **Slice 9 — MAUI istemci:** ✅ `maui-android` + `maui-windows` iş yükleri;
  `FinTrack.Mobile.Core` (net10.0), `FinTrack.Mobile` ve
  `FinTrack.Mobile.Tests`. Shell + MVVM, typed `HttpClient`, `SecureStorage`
  token akışı, 401 refresh + güvenli tekrar (POST/PUT otomatik retry yok),
  giriş/kayıt, özet, harcama + ekleme, kartlar + detay, borçlar, raporlar ve
  ayarlar. Token'lar loglanmaz. Hedefler: Android + Windows; çevrimdışı yazma
  ve bildirim kapsam dışı. Android emülatör smoke'u giriş → harcama → kart →
  rapor akışını doğruladı; eksik `MainApplication`, global `login` rotası,
  `Keyboard="Numeric"` ve root geri dönüş düzeltmeleri kalıcı hale getirildi.
  Geçici HTTP/cleartext ayarları geri alındı; varsayılan API adresi
  `https://10.0.2.2:7241/api/v1/`.
- **Testler:** ✅ V11-T01 sonrası toplam **435/435** yerel: 222 modül + 77 API
  birim + 10 mimari + 31 mobil + 95 entegrasyon (72 mevcut + 23 idempotency);
  host build 0 uyarı/0 hata. Ayrıntı: `docs/11` §9.5. Önceki yeşil CI run'ları
  `44365407221` (CI bakımı), `44366617692` (dokümanlar), `44379256250`
  (Android düzeltmeleri; Slice 9 run `44189257561`); V11-T01 kodu `5e614c7`
  için CI run `44413433056` **yeşil** (2026-11-18). Android MAUI head build
  JDK 17 ile yerelde 0 uyarı/0 hata doğrulandı; `FinTrack.Mobile.Core` ve
  `FinTrack.Mobile.Tests` de yeşil. Kaldığımız yer `docs/08` §8.5'te; Windows
  manuel UI smoke operatör görevidir.
- **Dokümanlar:** ✅ 01–11 + ADR-0010 tamam; `docs/02` §5 açık kararları ve kart
  alışverişi sahipliği (ADR-0008) kapandı. ADR-0009 (modüller arası atomik yazma)
  kabul edildi; ADR-0010 (v1.1 idempotent yazmalarda host sahipliğinde transaction)
  koşullu onay + açık kodlama onayıyla kabul edildi ve V11-T01'de uygulandı.
  `Idempotency-Key` uygulaması (`docs/06` §1.4, `docs/07` §5.1) tamamlandı;
  güncel durum `docs/08`, doğrulama `docs/11` §9.5, sıradaki operasyonel
  adımlar `docs/09`.
- **Migration:** `dotnet run --project src/Host/FinTrack.Api -- --migrate`
  (üretimde otomatik migration yok). Testlerde `Database:MigrateOnStartup=true`.
  Slice 4: `20261116180727_AddCardPaymentsAndStatements`. Slice 5:
  `20261116182933_InitialCashInflows`. Slice 6:
  `20261117174437_AddCashAdvances` (`CreditCardsDbContext` modeliyle senkron).
  Slice 7: `20261117180809_InitialDebts` (`DebtsDbContext` modeliyle senkron).
- **Sürüm kontrolü:** ✅ git (`main`) + GitHub uzak depo
  [`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack).
  GitHub Actions CI (`.github/workflows/ci.yml`) Slice 9 build + testlerini
  doğruladı: run `44365407221` (CI bakımı), `44366617692` (dokümanlar) ve
  `44379256250` (Android düzeltmeleri) yeşil (Android + Windows hedefleri,
  Java 17 + `dotnet workload restore`; 335/335; Slice 9 run `44189257561`,
  Slice 8 run `44156632893`, Slice 7 run `44150709461` idi). CI workflow'ü
  Node 24 action sürümleri ve sabit
  `ubuntu-24.04` görüntüsü kullanır (2026-11-18 bakımı; `actionlint` temiz).
- **Sıradaki:** v1 dilimleri (0–9) ve **V11-T01 (Idempotency-Key)** kod tarafında
  tamamlandı; Android emülatör smoke'u giriş → harcama → kart → rapor akışıyla
  doğrulandı. Kararlar [docs/10 v1.2](docs/10-karar-bekleyen-konular.md) ile
  alındı; **altı koşullu tasarım onayı** karşılandı:
  [docs/11 v1.3](docs/11-idempotency-teknik-tasarim.md) ve
  [ADR-0010](docs/adr/0010-host-owned-idempotent-writes.md). Görev kartı
  [docs/07 §5.1](docs/07-yol-haritasi-ve-ajan-gorev-standardi.md)'de. Açık
  kodlama onayı alındı; uygulama ve 435/435 doğrulama `docs/11` §9.5'te. Dört
  kritik konunun çözümü `docs/11` §9.2'de; kalan riskler §9.4. LocalDB smoke
  temizliği ✅ tamamlandı; CI run `44413433056` (`5e614c7`) yeşil; Android MAUI
  head build JDK 17 ile yerelde 0/0 doğrulandı. **Windows manuel UI smoke
  operatör görevi bekliyor.** Oturum devri kaydı `docs/08` §8.5'te; geri dönünce
  önce `docs/09` §2 ve `docs/08` §8.5 okunur. Idempotency sonrası v1.1 önceliği:
  parola sıfırlama + e-posta doğrulama.

## Çözüm yapısı (özet)

```
src/
├─ Host/FinTrack.Api                      tek dağıtım birimi
├─ BuildingBlocks/FinTrack.SharedKernel   Money, Entity, Result
├─ BuildingBlocks/FinTrack.ModuleContracts modüller arası sözleşmeler
├─ Modules/Expenses/…                     harcama, kategori
├─ Modules/CashInflows/…                  para girişi
├─ Modules/CreditCards/…                  kart, işlem, ekstre, nakit avans
├─ Modules/Debts/…                        kredi + kişisel borç
├─ Modules/Reporting/…                    aylık özet + borç özeti + yaklaşan ödemeler (salt okunur)
└─ Mobile/
   ├─ FinTrack.Mobile.Core                platform bağımsız istemci çekirdeği (net10.0)
   └─ FinTrack.Mobile                     .NET MAUI (Android + Windows)
```

Detay: [docs/03-mimari.md](docs/03-mimari.md)

## Geliştirme ajanları için bağlayıcı kurallar

Bu depoda çalışan bir ajan (Codex, Claude, vb.):

- Ürünü muhasebe, bankacılık, e-ticaret veya yatırım platformuna dönüştürecek kapsam genişletmesi **yapamaz**.
- Gerçek kart numarası / CVV / banka giriş bilgisi isteyen veya saklayan özellik **ekleyemez**.
- Talep edilmemiş banka API entegrasyonu **ekleyemez**.
- Harcama, ödeme, para girişi ve borcu tek bir genel işlem türüne **indirgeyemez**.
- Finansal hesaplamayı yalnızca dil modeline **bırakamaz**.
- Açık mimari kararları kendi varsayımıyla **kesinleştiremez**; önce sorar.
- Yeni teknik bağımlılık önerdiğinde **gerekçe yazmak zorundadır**.
- Modül sınırlarını ihlal eden kısa yol (cross-module DbContext, doğrudan tablo erişimi) **kullanamaz**.
