# FinTrack — Oturum Devir Notu (2026-11-21)

> Bu not, 2026-11-21 oturumu kapanırken bir sonraki oturumun **kaldığı yerden devam
> edebilmesi** için hazırlandı. İçerik `git status -sb` ve docs dosyalarının doğrudan
> okunmasıyla doğrulandı. Bu oturumda **commit/push yapılmadı**, production kodu
> değiştirilmedi.

---

## 1. Genel durum

- Depo: `C:\Users\Erdogan\source\FinTrack`, dal: `## main...origin/main`.
- Çalışma ağacı **temiz değil**: aşağıdaki docs/test dosyaları modified/untracked.
- Tamamlanan son iş: **V12-T01** (yalnızca test + dokümantasyon).
- Öncesinde: **Transaction Participant Architecture Audit** ve **Y-3'ün bağımsız
  doğrulanması** tamamlandı; Türkçe rapor masaüstüne ve `docs/` altına kaydedildi.
- Docker/Testcontainers gerektiren `FinTrack.Api.IntegrationTests` **çalıştırılmadı**.

---

## 2. Referans dosyalar — birleştirme tamamlandı

| Dosya | İçerik | Not |
|---|---|---|
| `docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md` | Orijinal audit gövdesi **+ §9 V12-T01 eki** (`## 9.` 288. satırda) | **Tek otorite dosya.** §9, `...-2026-11-20.md` kopyasından birebir aktarıldı. |
| `docs/FinTrack-V12-T01-Test-Delivery-2026-11-21.md` | V12-T01 teslim raporu (7 test dosyası, 379/379, açık bulgular) | Doğru ve güncel. |
| `C:\Users\Erdogan\Desktop\FinTrack-Transaction-Participant-Audit-2026-11-21.md` | Yalnızca orijinal audit gövdesi | Masaüstü kopyası güncellenmedi; güncel sürüm depoda. |
| `C:\Users\Erdogan\Desktop\FinTrack-V12-T01-Test-Delivery-2026-11-21.md` | Teslim raporunun masaüstü kopyası | Masaüstü kopyası. |

**Sonuç / çözüm:** Başlangıçta `docs/08` §8.8 §9'u `...-2026-11-21.md` olarak
işaret ediyordu; ancak ek `...-2026-11-20.md` içindeydi. Kullanıcı onayıyla iki
dosya tek dosyada birleştirildi: §9 depodaki `...-2026-11-21.md` içine eklendi,
`...-2026-11-20.md` silindi. `docs/08`, `docs/09` ve teslim raporundaki tüm
işaretçiler bu tek dosyaya yönlendirildi. Audit bulgularının içeriği değişmedi.

---

## 3. Audit tarafında varılan sonuçlar

- **Y-3 doğrulandı, ama yalnızca yapısal fark olarak.** Expenses/CreditCards
  `UnitOfWork.cs:51-88` modül-yerel `ITransactionScopeFactory` + `HostTransactionScope`
  içerir; CashInflows/Debts `UnitOfWork.cs:9-48` yalnızca `Enlist`/`SaveChanges`
  yapar. Bu farkın **atomiklik hatasına yol açtığı kanıtlanmadı**; host yolunda
  handler commit'leri no-op (`SqlWriteTransactions.cs:80-87`).
- Dört modül aynı `IWriteScopeParticipant` sözleşmesini uygular
  (`FinTrack.ModuleContracts/AtomicWrites.cs:46-49`).
- Gerçek commit noktaları: `IdempotencyMiddleware.cs:259` (2xx) ve `:345`
  (expired→tombstone). `committed` değişkeni atanıp okunmuyor (F-03).
- **Öneri: Seçenek C** — mevcut host-merkezi koordinasyonu test ve küçük
  temizliklerle pekiştirmek. A koşullu kabul edilebilir; **B (ortak fabrika
  refactor'ı) kanıtlanmış hata olmadığı için gerekçesiz, ertelendi**.
- **S25 yanıltıcı:** "dört modül aynı transaction" adına karşın gövdesi dört ayrı
  POST + dört ayrı `Guid.NewGuid()` (`IdempotencyCoreScenariosTests.cs:301-342`);
  çok modüllü tek transaction kanıtı olarak gösterilmemeli.
- Audit raporundaki **Görev 1–6** (öncelik sırasıyla) raporun §7'sinde duruyor:
  1. host sahipliği için zorlayıcı mimari testler, 2. `committed`/iki commit
  noktası temizliği, 3. connection/transaction kimliği + çift-enlist testleri,
  4. hata senaryoları (Enlist istisnası, ikinci participant, gerçek commit
  istisnası, rollback), 5. S25 adı + F-13 yorum, 6. Y-3 mirasının birleştirilmesi
  (opsiyonel, yüksek risk).

---

## 4. V12-T01'de gerçekten yapılanlar

### Test dosyaları (7)

| Dosya | Değişiklik |
|---|---|
| `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs` | 4 yeni test (7 vaka): contract sınırı, `Enlist` imzası + host fallback, ham SqlClient yasağı, `BeginTransaction` yalnızca mevcut yerel fabrikada (allowlist ile yanlış pozitif yok) |
| `tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs` | Production `SqlWriteTransactionScopeFactory`'nin kapalı bağlantıyı açıp `IsolationLevel.Serializable` istediğini fake `DbConnection` ile kanıtlayan test + no-op scope testi; kayıtlı connection test çifti |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs` | Gerçek SQLite'ta owner/writer `GetDbConnection()` + `CurrentTransaction.GetDbTransaction()` referans eşitliği |
| `tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs` | Aynı kimlik invariant'ı (gerçek SQLite) |
| `tests/FinTrack.Modules.Expenses.Tests/Application/UnitOfWorkEnlistTests.cs` | **Yeni** — Expenses `Enlist` guard'ı (çift enlist, `Enlist(null)` ± host accessor) |
| `tests/FinTrack.Modules.CreditCards.Tests/Application/UnitOfWorkEnlistTests.cs` | **Yeni** — CreditCards + CashInflows guard'ı |
| `tests/FinTrack.Modules.Debts.Tests/Application/UnitOfWorkEnlistTests.cs` | **Yeni** — Debts + CashInflows guard'ı |

Not: CashInflows, `InternalsVisibleTo` nedeniyle iki test projesinde doğrulanır;
Expenses yalnızca kendi projesinde test edilebilir.

### Dokümantasyon

- `docs/08-durum-ve-devam-notu.md` → v2.16, §8.8
- `docs/09-todo.md` → v1.26, V12-T01 bölümü
- Audit raporu + §9 eki (bkz. §2 uyuşmazlığı)

### Doğrulama sonuçları

- `dotnet build FinTrack.slnx --no-restore` → **0 uyarı / 0 hata**
- Docker hariç testler: Architecture 17/17, CreditCards 85/85, Debts 38/38,
  Api.Unit 78/78, Expenses 58/58, CashInflows 48/48, Reporting 10/10, Mobile 45/45
  → **toplam 379/379** (0 başarısız, 0 atlanan; önceki 354/354 + 25 yeni test)
- **Docker/Testcontainers API entegrasyon testleri çalıştırılmadı** (S18, S25,
  S5/S6/S21 ve tüm gerçek MSSQL senaryoları doğrulanmadı).

### Kanıtlanmayanlar (sonraki iş için)

- `UseTransaction`'ın kaç kez çağrıldığı doğrudan sayılmadı; `_enlisted` referansı +
  `CurrentTransaction` kararlılığı + başarılı commit ile dolaylı destek var.
- SQLite test çifti `IsolationLevel` belirtmiyor; `Serializable` yalnızca production
  host fabrikasında fake connection ile kanıtlandı.
- Kaynak-metin tabanlı mimari testleri kırılgandır; yeni dosya/desene göre
  güncellenmeleri gerekir.
- 3+ modülün tek istekte aynı transaction'a katılımı kanıtlanmadı.
- `DuringCommit` hook'u gerçek `DbTransaction.CommitAsync`'ten önce atar; gerçek
  commit istisnası hâlâ test edilmedi.

---

## 5. Devam eden kısıtlar (yeni oturumda da geçerli)

- Production kodu değiştirme: **yok** (yalnızca `tests/**` + `docs/**`).
- Yeni abstraction/interface, transaction factory refactoring: **yok**.
  Expenses/CreditCards yerel fabrikaları kasıtlı olarak korundu.
- NuGet paketi / migration: **yok**.
- Mevcut testleri silme/zayıflatma: **yok**, yalnızca ekleme.
- Commit / push: **yapılmadı**; yalnızca açıkça istenirse yapılmalı.
- Docker gerektiren testler bu oturumda çalıştırılmadı; ortamda Docker daemon
  geçmiş oturumlarda erişilemezdi (`npipe://./pipe/docker_engine`).

---

## 6. Git durumu (oturum kapanış anlık görüntüsü)

```
## main...origin/main
 M docs/08-durum-ve-devam-notu.md
 M docs/09-todo.md
 M tests/FinTrack.Api.UnitTests/WriteTransactionInternalsTests.cs
 M tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs
 M tests/FinTrack.Modules.CreditCards.Tests/Application/CashAdvanceAtomicityTests.cs
 M tests/FinTrack.Modules.Debts.Tests/Application/AtomicWriteTests.cs
?? docs/FinTrack-Transaction-Participant-Audit-2026-11-20.md
?? docs/FinTrack-Transaction-Participant-Audit-2026-11-21.md
?? docs/FinTrack-V12-T01-Test-Delivery-2026-11-21.md
?? docs/OTURUM-DEVIR-NOTU-2026-11-21.md
?? tests/FinTrack.Modules.CreditCards.Tests/Application/UnitOfWorkEnlistTests.cs
?? tests/FinTrack.Modules.Debts.Tests/Application/UnitOfWorkEnlistTests.cs
?? tests/FinTrack.Modules.Expenses.Tests/Application/UnitOfWorkEnlistTests.cs
```

---

## 7. Sıradaki adımlar (önerilen sıra)

1. ~~**Docs gösterge düzeltmesi + audit birleştirme:** §9 işaretçilerini düzeltin,
   iki audit dosyasını tek dosyada birleştirin~~ ✅ **yapıldı** (kullanıcı onayıyla):
   §9 depodaki `...-2026-11-21.md` içine eklendi, `...-2026-11-20.md` silindi;
   `docs/08`, `docs/09` ve teslim raporundaki işaretçiler güncellendi.
2. **Docker doğrulaması (kullanıcı isterse):** `tests/FinTrack.Api.IntegrationTests`
   çalıştırın; daemon yoksa "çalıştırılmadı" notunu koruyun.
3. **Açık audit bulguları ayrı bir görevde:** önce yüksek seviyeli F-06 (Enlist
   istisnası politikası), F-07 (gerçek commit istisnası), F-08 (gerçek ikinci
   participant hatası); ardından F-03/F-04 (`committed` ölü flag + iki commit
   noktası), F-05 (S25 adı), F-13 (güncel olmayan yorum), F-11 (PUT/auth yolları),
   F-14 (rollback mekanizması). **F-12 kapalı**, F-01/F-09/F-10 kısmen kapalı.
4. **Windows manuel UI smoke** hâlâ operatörde bekliyor (V11-T02'den devir).
5. Commit/push yalnızca kullanıcı açıkça isterse.

---

## 8. Faydalı komutlar

```powershell
git status -sb
dotnet build FinTrack.slnx --no-restore
dotnet test tests/FinTrack.ArchitectureTests --nologo
dotnet test tests/FinTrack.Api.UnitTests --nologo
dotnet test tests/FinTrack.Modules.CreditCards.Tests --nologo
dotnet test tests/FinTrack.Modules.Debts.Tests --nologo
dotnet test tests/FinTrack.Modules.Expenses.Tests --nologo
dotnet test tests/FinTrack.Modules.CashInflows.Tests --nologo
dotnet test tests/FinTrack.Modules.Reporting.Tests --nologo
dotnet test tests/FinTrack.Mobile.Tests --nologo
# Docker gerektirir, bu oturumda çalıştırılmadı:
dotnet test tests/FinTrack.Api.IntegrationTests --nologo
```

PowerShell konsolunda Türkçe karakterler bozuk görünebilir; dosya içerikleri UTF-8
ve sağlamdır (doğrulamak için `read` aracını kullanın).
