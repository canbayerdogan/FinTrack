# FinTrack

[![CI](https://github.com/canbayerdogan/FinTrack/actions/workflows/ci.yml/badge.svg)](https://github.com/canbayerdogan/FinTrack/actions/workflows/ci.yml)

Kişisel finans ve harcama takibi uygulaması.

**Nihai ürün:** mobil uygulama (.NET MAUI)
**Backend:** ASP.NET Core Web API · SQL Server · .NET 10
**Mimari:** modüler monolit · modül içi Clean Architecture · CQRS (mediator'süz)

> Bu depo şu anda **tasarım + iskelet + kimlik doğrulama + harcama/kategori +
> kredi kartı + ekstre/ödeme + para girişi + nakit avans + kredi/kişisel borç**
> aşamasındadır. Çözüm iskeleti (slice 0), kimlik doğrulama (slice 1),
> harcama/kategori (slice 2), kart + hesaplanan bakiye (slice 3), ekstre + kart
> ödemesi (slice 4), para girişi (slice 5), nakit avans (slice 6) ve kredi/kişisel
> borç (slice 7) derlenir ve testleri geçer; raporlama (slice 8) ve MAUI istemci
> (slice 9) henüz yazılmadı. Kod yazılmadan önce dokümanların okunması zorunludur.

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
| 05 | [İş kuralları (business rules) v1.2](docs/05-is-kurallari.md) | ✅ |
| 06 | [API sözleşmeleri v1.4](docs/06-api-sozlesmeleri.md) | ✅ |
| 07 | [Yol haritası ve ajan görev standardı v1.11](docs/07-yol-haritasi-ve-ajan-gorev-standardi.md) | ✅ |
| 08 | [Durum ve devam notu v2.1](docs/08-durum-ve-devam-notu.md) | ✅ |
| 09 | [Görev listesi (TODO) v1.10](docs/09-todo.md) | ✅ |

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
- **Testler:** ✅ toplam **300/300** (237 birim/mimari + 63 entegrasyon); CI run
  `44150709461` yeşil (Ubuntu + Testcontainers MSSQL). Birim/mimari dağılımı:
  3 mimari + 22 birim + 55 Expenses + 78 CreditCards + 48 CashInflows + 31
  Debts. Yerel Docker motoru kapalıydı; entegrasyon testleri CI'da doğrulandı.
- **Dokümanlar:** ✅ 01–09 tamam; `docs/02` §5 açık kararları ve kart alışverişi
  sahipliği (ADR-0008) kapandı. ADR-0009 (modüller arası atomik yazma) kabul
  edildi. `Idempotency-Key` uygulaması ayrı göreve ertelendi (`docs/06` §1.4,
  `docs/07` slice 2). Güncel durum: `docs/08`; sıradaki adımlar: `docs/09`.
- **Migration:** `dotnet run --project src/Host/FinTrack.Api -- --migrate`
  (üretimde otomatik migration yok). Testlerde `Database:MigrateOnStartup=true`.
  Slice 4: `20261116180727_AddCardPaymentsAndStatements`. Slice 5:
  `20261116182933_InitialCashInflows`. Slice 6:
  `20261117174437_AddCashAdvances` (`CreditCardsDbContext` modeliyle senkron).
  Slice 7: `20261117180809_InitialDebts` (`DebtsDbContext` modeliyle senkron).
- **Sürüm kontrolü:** ✅ git (`main`) + GitHub uzak depo
  [`canbayerdogan/FinTrack`](https://github.com/canbayerdogan/FinTrack).
  GitHub Actions CI (`.github/workflows/ci.yml`) Slice 7 build + testlerini
  doğruladı: run `44150709461` yeşil (300/300; Slice 6 run `44141605019` idi).
- **Sıradaki:** slice 8 — raporlama (`docs/07` §4). Slice 7 uçtan uca tamamlandı;
  borç ve para girişi verileri raporlama için hazır.

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
