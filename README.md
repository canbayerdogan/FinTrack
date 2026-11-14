# FinTrack

Kişisel finans ve harcama takibi uygulaması.

**Nihai ürün:** mobil uygulama (.NET MAUI)
**Backend:** ASP.NET Core Web API · SQL Server · .NET 10
**Mimari:** modüler monolit · modül içi Clean Architecture · CQRS (mediator'süz)

> Bu depo şu anda **tasarım + iskelet + kimlik doğrulama + harcama/kategori +
> kredi kartı** aşamasındadır. Çözüm iskeleti (slice 0), kimlik doğrulama (slice 1),
> harcama/kategori (slice 2) ve kart + hesaplanan bakiye (slice 3) derlenir ve
> testleri geçer; diğer iş modülleri (slice 4+) henüz yazılmadı. Kod yazılmadan
> önce dokümanların okunması zorunludur.

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
| 05 | [İş kuralları (business rules) v1.0](docs/05-is-kurallari.md) | ✅ |
| 06 | [API sözleşmeleri v1.2](docs/06-api-sozlesmeleri.md) | ✅ |
| 07 | [Yol haritası ve ajan görev standardı v1.5](docs/07-yol-haritasi-ve-ajan-gorev-standardi.md) | ✅ |
| 08 | [Durum ve devam notu v1.4](docs/08-durum-ve-devam-notu.md) | ✅ |
| 09 | [Görev listesi (TODO) v1.3](docs/09-todo.md) | ✅ |

## Mevcut durum

- **Slice 0 — İskelet:** ✅ 27 proje, `dotnet build` 0 hata, 0 uyarı.
- **Slice 1 — Kimlik doğrulama:** ✅ ASP.NET Core Identity + JWT, rotating
  refresh token (DB'de hash'li), `identity` şeması/migration, `POST /auth/register`,
  `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET/PUT /auth/me`.
  22 birim + 17 entegrasyon kimlik testi.
- **Slice 2 — Harcama + kategori:** ✅ `expenses` şeması/migration, harcama,
  iade ve kategori uç noktaları (`docs/06` §3–4). Kart alışverişi `CardId` ile
  aynı harcama kaydıdır (ADR-0008). 52 modül + 10 entegrasyon testi.
- **Slice 3 — Kart + hesaplanan bakiye:** ✅ `cards` şeması/migration, kart
  CRUD + arşivleme, `GET /cards/{id}/balance` (harcama − iade; ödeme ve nakit
  avans sonraki dilimlerde). Bilinmeyen kart alanları (kart no/CVV/SKT) `400`
  ile reddedilir. 22 modül + 6 entegrasyon testi.
- **Testler:** ✅ 139 test yeşil — Debug ve Release (3 mimari + 22 birim + 52
  Expenses + 22 CreditCards + 40 entegrasyon; 7 smoke + 17 kimlik + 10 harcama +
  6 kart). Entegrasyon testleri için Docker Desktop açık olmalı.
- **Dokümanlar:** ✅ 01–09 tamam; `docs/02` §5 açık kararları ve kart alışverişi
  sahipliği (ADR-0008) kapandı. `Idempotency-Key` uygulaması ayrı göreve ertelendi
  (`docs/06` §1.4, `docs/07` slice 2). Güncel durum: `docs/08`; sıradaki adımlar:
  `docs/09`.
- **Migration:** `dotnet run --project src/Host/FinTrack.Api -- --migrate`
  (üretimde otomatik migration yok). Testlerde `Database:MigrateOnStartup=true`.
- **Sürüm kontrolü:** ✅ git deposu başlatıldı (başlangıç commit'i). GitHub Actions
  CI eklendi (`.github/workflows/ci.yml`); uzak depo bağlanınca ilk çalıştırma
  doğrulanacak.
- **Sıradaki:** slice 4 — ekstre + kart ödemesi (`docs/07` §4).

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
├─ Modules/Reporting/…                    aylık özet (salt okunur)
└─ Mobile/FinTrack.Mobile                 .NET MAUI
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
