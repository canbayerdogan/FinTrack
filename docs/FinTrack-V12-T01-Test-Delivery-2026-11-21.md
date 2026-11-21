# FinTrack — V12-T01: Host Transaction Ownership Enforcement Tests

**Teslim raporu · 2026-11-21**

> Bu rapor yalnızca V12-T01 kapsamında **gerçekten tamamlanan** test değişikliklerini,
> doğrulama sonuçlarını ve açık kalan audit bulgularını kaydeder. Production koduna
> dokunulmadı; commit/push yapılmadı. Docker/Testcontainers gerektiren API
> entegrasyon testleri çalıştırılmadı.
>
> Referans: `docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` (orijinal denetim
> + §9 V12-T01 eki).

---

## 1. Değiştirilen / eklenen dosyalar

### Test dosyaları (7)

| Dosya | Değişiklik |
|---|---|
| `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs` | 4 yeni test (7 test vakası) |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs` | +1 gerçek SQLite kimlik testi |
| `tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs` | +1 gerçek SQLite kimlik testi |
| `tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs` | +1 host `Serializable` testi, kayıtlı `DbConnection` test çifti |
| `tests/FinTrack.Modules.Expenses.Tests/Application/UnitOfWorkEnlistTests.cs` | **yeni** — Expenses `Enlist` guard |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/UnitOfWorkEnlistTests.cs` | **yeni** — CreditCards + CashInflows `Enlist` guard |
| `tests/FinTrack.Modules.Debts.Tests/Application/UnitOfWorkEnlistTests.cs` | **yeni** — Debts + CashInflows `Enlist` guard |

Not: dört `UnitOfWork` kapsanmıştır. CashInflows, `InternalsVisibleTo` nedeniyle hem
CreditCards hem Debts test projelerinde doğrulanır; Expenses yalnızca kendi test
projesinde doğrulanabilir.

### Dokümantasyon (3)

| Dosya | Değişiklik |
|---|---|
| `docs/08-durum-ve-devam-notu.md` | v2.16, §8.8 V12-T01 notu, değişiklik geçmişi satırı 2.16 |
| `docs/09-todo.md` | v1.26, V12-T01 bölümü, son doğrulama ve geçmiş satırı 1.26 |
| `docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` | Rapor `docs/` altına alındı; §9 V12-T01 eki bu dosyada birleştirildi, eski `...-2026-11-20.md` kopyası silindi |

Mevcut testler silinmedi veya anlamları zayıflatılmadı; yalnızca ekleme yapıldı.

---

## 2. Yeni testlerin doğruladığı mimari garantiler

| Test | Garanti | Audit bulgusu |
|---|---|---|
| `Transaction_sozlesmeleri_buildingblocks_ta_ve_dort_modulde_baglanti_var` | 6 transaction sözleşmesinin `FinTrack.ModuleContracts` içinde tanımlı olduğu ve dört modül Infrastructure derlemesinin bu contract'a bağlı olduğu | F-10 |
| `UnitOfWork_Enlist_imzasi_ve_host_fallback_kaynakta_korunuyor` | Dört UoW'da `Enlist(IWriteTransactionScope)` imzası, `scopeAccessor?.Current` fallback'i ve `UseTransaction` katılım mekanizması | F-01, F-12 |
| `Moduller_ham_SqlClient_bagintisi_veya_transaction_acmasi_yapmiyor` | Dört modülde `new SqlConnection` / `SqlConnection(` / `new SqlTransaction` / `SqlTransaction(` olmadığı (ham SqlClient ile host atlama yok) | F-01, F-10 |
| `Transaction_baslatma_sadece_mevcut_yerel_fabrikada` | CashInflows/Debts'ta hiç `BeginTransaction` olmadığı; Expenses/CreditCards'ta ise yalnızca mevcut yerel `TransactionScopeFactory` içinde ve host-scope guard'ıyla başlatıldığı — **mevcut tasarım ihlal sayılmadı, yanlış pozitif üretilmedi** | F-01, F-10 |
| `Enlist_sirada_owner_ve_writer_ayni_fiziksel_transaction_a_bagli` (2 dosya) | Gerçek SQLite'ta owner ve writer DbContext'lerinin `Database.GetDbConnection()` referansları eşit; her iki `Database.CurrentTransaction.GetDbTransaction()` değeri `scope.Transaction` ile aynı referans; enlist sonrası bağlantı kullanılabilir | **F-09** |
| `Gercek_host_fabric_bagantiyi_Serializable_transaction_ile_acar` | Production `SqlWriteTransactionScopeFactory` kapalı bağlantıyı açıyor, `IsolationLevel.Serializable` istiyor ve gerçek `WriteTransactionScope` döndürüyor — SQL Server/Docker olmadan | F-09 |
| `Ayni_transaction_icin_ikinci_enlist_baglantiyi_bozmaz` (dört modül) | Aynı transaction ikinci kez enlist edildiğinde `CurrentTransaction` ve `_enlisted` referansı değişmiyor; transaction hâlâ commit edilebiliyor | **F-12** |
| `Enlist_null_host_scope_yoksa_transaction_dan_ayrir` (dört modül) | Accessor yokken `Enlist(null)` DbContext'i transaction'dan ayırıyor | F-12 |
| `Enlist_null_host_scope_varsa_hosta_duser_sonra_temizlenince_ayrilir` (dört modül) | Host accessor doluyken `Enlist(null)` host transaction'ına düşüyor; accessor temizlenince `Enlist(null)` ayırıyor | F-12 |

---

## 3. Test / build sonuçları

- `dotnet build FinTrack.slnx --no-restore` → **0 uyarı / 0 hata**
- Çalıştırılan test projeleri (Docker gerektirmeyen):
  - Mimari: **17/17**
  - CreditCards modülü: **85/85**
  - Debts modülü: **38/38**
  - Api.UnitTests: **78/78**
  - Expenses modülü: **58/58**
  - CashInflows modülü: **48/48**
  - Reporting modülü: **10/10**
  - Mobile: **45/45**
- **Toplam: 379/379 geçti**, 0 başarısız, 0 atlanan (önceki doğrulama 354/354; +25 yeni test)
- **Docker/Testcontainers gerektiren `FinTrack.Api.IntegrationTests` çalıştırılmadı.**
  S18, S25, S5/S6/S21 dahil tüm gerçek MSSQL senaryoları bu oturumda doğrulanmadı.

---

## 4. Testlerin kanıtlamadığı davranışlar

- `Enlist` guard'ının `UseTransaction` çağrısını **kaç kez** yaptığı doğrudan sayılmadı;
  "gereksiz yeniden enlist yok" iddiası `_enlisted` referansı + `CurrentTransaction`
  kararlılığı + başarılı commit ile **dolaylı** olarak desteklenir.
- SQLite test çifti `IsolationLevel` belirtmediği için SQLite tarafında `Serializable`
  iddiası test edilmedi; yalnızca **production host fabrikasının** `Serializable`
  istediği fake connection ile kanıtlandı. Gerçek SQL Server davranışı Docker
  olmadan doğrulanamadı.
- Kaynak-metin tabanlı mimari testleri kırılgandır; yeni dosya/desene göre
  güncellenmeleri gerekir. Çalışma zamanındaki tüm olası host atlama yollarını
  kapsamaz.
- Dört modülün **tek bir istekte aynı transaction'a** katıldığı (3+ modül senaryosu)
  bu testlerle kanıtlanmadı; yalnızca owner+writer çifti ve mimari kurallar doğrulandı.

---

## 5. Production koduna dokunulup dokunulmadığı

**Dokunulmadı.** `git status -sb` yalnızca `tests/**` ve `docs/**` değişikliklerini
gösteriyor:

```
 M docs/08-durum-ve-devam-notu.md
 M docs/09-todo.md
 M tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs
 M tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs
 M tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs
 M tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs
?? docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md
?? tests/FinTrack.Modules.CreditCards.Tests/Application/UnitOfWorkEnlistTests.cs
?? tests/FinTrack.Modules.Debts.Tests/Application/UnitOfWorkEnlistTests.cs
?? tests/FinTrack.Modules.Expenses.Tests/Application/UnitOfWorkEnlistTests.cs
```

- Yeni abstraction/interface: **yok**
- Transaction factory refactoring: **yok** (Expenses/CreditCards yerel fabrikaları korundu)
- NuGet paketi / migration: **yok**
- Commit / push: **yok**

---

## 6. Açık kalan audit bulguları

| Bulgu | V12-T01 sonrası durum |
|---|---|
| **F-01** (Orta) | Kısmen kapatıldı: sapma tespiti zorlayıcı hale getirildi; yapısal fark kasıtlı olarak korunuyor. |
| **F-09** (Orta) | Kısmen kapatıldı: connection/transaction kimliği ve host `Serializable` isteği test edildi; 3+ modül tek transaction'ı hâlâ kanıtlanmadı. |
| **F-10** (Orta) | Kısmen kapatıldı: dört modül için mimari koruma eklendi; PUT/auth kapsam dışı yollar açık. |
| **F-12** (Düşük) | **Kapatıldı:** guard davranışı dört modül için test edildi. |
| F-02, F-03, F-04, F-05, F-06, F-07, F-08, F-11, F-13, F-14 | **Açık** — bu görev kapsam dışı. |

Açık maddelerin özü: gerçek ikinci participant hatası, gerçek
`DbTransaction.CommitAsync` istisnası, `Enlist` istisnası, S25'in yanıltıcı kapsamı,
`committed` ölü flag'i ve iki commit noktası, güncel olmayan yorum, açık rollback
mekanizmasına dayalı davranış ve Docker ortamında API entegrasyon doğrulaması.

---

## Genel sonuç

V12-T01, host-merkezi transaction koordinasyonunu **test düzeyinde zorunlu kılan**
korumaları tamamladı: dört modulunda contract sınırı, ham SqlClient atlama yasağı,
gerçek SQLite connection/transaction kimliği, `Enlist` guard davranışı ve production
host fabrikasının `Serializable` isteği doğrulandı. Expenses/CreditCards yerel
fabrikaları mevcut tasarım olarak korundu ve ihlal işareti almadı. Sonuç:
**Docker hariç 379/379 test, 0 uyarı/0 hata build**, production kodu değişmeden,
commit/push olmadan teslim edildi. Gerçek MSSQL ve kalan audit bulguları açık olmaya
devam ediyor.
