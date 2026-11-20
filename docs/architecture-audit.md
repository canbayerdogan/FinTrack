# Mimari ve Kod Kalitesi Denetimi (Architecture Audit v1)

- **Tarih:** 2026-11-19
- **Kapsam:** Kaynak kod ve testler (statik inceleme). Mimari dokümanlar yalnızca kod ile karşılaştırma için referans alınmıştır.
- **Kısıtlar:** Bu görevde kod, test, şema veya veri değiştirilmedi; refactoring/commit/push yapılmadı.
- **Doğrulama bağlamı (kayıt):** Yerel çalışma kopyası `main` `88441ac` (un-pushed, `origin/main` `ce412cd` üzerinde 1 commit önde), çalışma ağacı temiz. CI koşusu `44413433056` (`5e614c7`) yeşil; mobil testler `31/31` ve Android MAUI derlemesi (JDK 17) bu oturumda yerelde doğrulandı. Tam entegrasyon paketi bu oturumda Docker kapalı olduğu için yerelde tekrar koşulmadı; Windows manuel UI smoke (karar 2.1-A) operatör adımı olarak yürütülmedi ve başarılı sayılmamıştır. *(Bu maddeler denetim bulgusu değil, inceleme bağlamıdır.)*

---

## 1. Yönetici özeti

FinTrack, .NET tabanlı modüler monolit + MAUI mobil istemci yapısında, modül sınırlarını proje referansları, `ModuleContracts`/`SharedKernel` sözleşmeleri ve mimari testlerle görece iyi koruyan bir kod tabanına sahiptir. Host, kimlik, idempotency ve paylaşılan yazma transaction altyapısını üstlenmekte; modüller kendi Domain/Application/Infrastructure/Endpoints katmanlarına ve şema/DbContext sahipliğine ayrılmış durumdadır (doğrulanan örnekler: `src/Host/FinTrack.Api/Program.cs:140-146`, `:173-176`, `src/BuildingBlocks/FinTrack.ModuleContracts/AtomicWrites.cs`, `src/Host/FinTrack.Api/WriteTransactions/SqlWriteTransactions.cs`, `src/Modules/*/Infrastructure/*DbContext*.cs`, `tests/FinTrack.ArchitectureTests/LayerBoundaryTests.cs`, `AtomicWriteBoundaryTests.cs`).

**Genel değerlendirme:** Mimari, v1 kapsamı ve mevcut modül sayısı için **iyi/olgunlaşan** seviyededir; özellikle sınır koruması, atomik çapraz modül yazma ve idempotency tasarımı güçlüdür. Buna karşın büyüme altında dikkat gerektiren başlıca noktalar: raporlama sorgularının bellek içi/çok turlu veri işleme yaklaşımı, mobil tarafta pending-operation/idempotency altyapısının gerçek yazma akışlarına tam entegrasyonunun sınırlı görünmesi, bazı desen tutarsızlıkları ve çoklu para birimi/e-posta doğrulama gibi çapraz kesen gereksinimler için hazırlık eksikleridir.

**Varsayım/kanıt ayrımı:** Aşağıdaki bulgular, aksi belirtilmedikçe okunan kaynak dosyalara ve test/dosya envanterine dayanır. Çalışma zamanı performansı, üretim yükü davranışı ve manuel UI akışları bu denetimde ölçülmemiştir.

---

## 2. Mevcut mimarinin gerçek durumu

### 2.1 Çözüm ve proje yapısı
- Çözüm: `FinTrack.slnx` — BuildingBlocks, Host, Mobile, 5 iş modülü (Expenses, CashInflows, CreditCards, Debts, Reporting) ve test projeleri.
- Ortak ayarlar: `Directory.Build.props` (`net10.0`, nullable, implicit usings; stil/analiz uyarıları hata yapılmıyor, sınır ihlali esasen proje referanslarıyla engelleniyor — satır 15-16).
- Host: `src/Host/FinTrack.Api/FinTrack.Api.csproj` modül Endpoint+Infrastructure projelerine ve BuildingBlocks'a referans verir; modül Application/Domain tiplerini doğrudan referanslamaz.
- BuildingBlocks: `FinTrack.SharedKernel` bağımsız; `FinTrack.ModuleContracts` yalnızca `SharedKernel`'a bağımlı (`src/BuildingBlocks/.../*.csproj`).
- Modül katmanları (örneklenen projeler): `Domain -> SharedKernel`; `Application -> Domain + SharedKernel + ModuleContracts`; `Infrastructure -> Application + SharedKernel (+ gerekli ModuleContracts)`; `Endpoints -> Application + Infrastructure` ve `FrameworkReference Microsoft.AspNetCore.App`.
- Mobil: `FinTrack.Mobile` (MAUI) -> `FinTrack.Mobile.Core`; Core dış HTTP/SQLite/MVVM bağımlılıkları içerir (`src/Mobile/FinTrack.Mobile.Core/FinTrack.Mobile.Core.csproj`, `.../FinTrack.Mobile.csproj`).

### 2.2 Bağımlılık yönü ve sınırlar (doğrulanan)
- Domain altyapı bağımlılığı yok (mimari test): `tests/FinTrack.ArchitectureTests/LayerBoundaryTests.cs:44-58`.
- Application kendi Infrastructure'una/EF'e bağımlı değil (test): `LayerBoundaryTests.cs:60-74`.
- Modüller birbirinin derlemelerine bağımlı değil (test): `LayerBoundaryTests.cs:76-93`.
- Reporting başka modülün Domain/Application/Infrastructure/EF/Data tiplerine bağımlı değil ve `ModuleContracts` referansı var (test): `LayerBoundaryTests.cs:95-129`.
- Modül kodu `FinTrack.Api`/host idempotency tiplerine bağımlı değil, yazma modülü UoW'ları `IWriteScopeParticipant` uygular, bazı modüllerde host-scope varken no-op transaction factory dalı kaynakta mevcut (test): `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs:48-135`.
- Tüm modül `MapPost` rotaları auth altında veya idempotency middleware allowlist'inde olmalı (kaynak taraması): `tests/FinTrack.ArchitectureTests/MapPostAllowlistTests.cs`, allowlist kaynağı `src/Host/FinTrack.Api/Idempotency/IdempotencyMiddleware.cs:21-29`.

### 2.3 Çalışma zamanı kompozisyonu
- DI ve pipeline: `Program.cs:59-78` (paylaşılan bağlantı/transaction, idempotency context/store/cleanup/hooks, migration kaydı), `:140-146` modül kayıtları, `:149-176` migration ve middleware sırası, `:176` `IdempotencyMiddleware` (auth sonrası), `:186-192` endpoint eşlemeleri.
- Her modül `Add*Module` ile Application+Infrastructure kaydı yapar; Reporting yalnızca Application kaydeder ve migration/DbContext içermez (`src/Modules/*/Endpoints/*Module.cs`, `src/Modules/Reporting/FinTrack.Modules.Reporting.Endpoints/ReportingModule.cs:18-24`).
- Çapraz modül okuma/yazma sözleşmeleri: `IExpenseReportQuery`, `ICashInflowReportQuery`, `ICardReportQuery`, `IDebtReportQuery`, `ICardExpenseQuery`, `ICardQuery`, `IDebtCashInflowWriter` vb. (`src/BuildingBlocks/FinTrack.ModuleContracts/*.cs`). Uygulamalar sahip modüllerde kayıtlı (`ExpensesApplicationRegistration.cs:21`, `CashInflowsApplicationRegistration.cs:24-27`, `CreditCardsApplicationRegistration.cs:24`, `DebtsApplicationRegistration.cs:19-20`).

### 2.4 Veri ve transaction modeli
- Modül başına şema/DbContext ve migration (dosya envanteri: `src/Modules/*/Infrastructure/*DbContext.cs`, `Configurations/*`, `Migrations/*`); host tarafında `shared` şemasıyla idempotency ve `identity` şeması (`IdempotencyDbContext.cs:12-22`, identity DbContext).
- ADR-0009/0010: tek fiziksel bağlantı (`SqlWriteConnectionProvider`), host scope accessor/setter, modül UoW'ları `Enlist` ile katılır, host scope varken modül yerel transaction'ı no-op sarmalanır (`WriteTransactions/SqlWriteTransactions.cs:38-86`, `Expenses.Infrastructure/Persistence/UnitOfWork.cs:16-87`, `CreditCards.Infrastructure/Persistence/UnitOfWork.cs`, `CashInflows/Debts.Infrastructure/Persistence/UnitOfWork.cs`).
- Idempotency middleware: kapsamlı POST'larda gövde hash/scope, kayıt ekleme veya mevcut kaydı değerlendirme, 2xx yanıtı tamamlama + tek commit, replay/409/410/202 akışları, bellek içi aktif anahtar kilidi (`IdempotencyMiddleware.cs:31-392`).

### 2.5 Mobil mimari
- Core: typed API client, auth handler, session refresher, pending-operation store (SQLite + AES-GCM, AAD), recovery servisi ve ViewModel'ler (`src/Mobile/FinTrack.Mobile.Core/...`).
- MAUI DI ve platform servisleri `MauiProgram.cs:30-97`; token/pending store güvenli depolama soyutlamaları üzerinden kullanılır.

---

## 3. Güçlü yönler

| # | Önem | Dosya yolu (kanıt) | Mevcut davranış | Oluşturduğu fayda / etki | Öneri | Zorunlu mu? |
|---|---|---|---|---|---|---|
| G-1 | Bilgi | `tests/FinTrack.ArchitectureTests/LayerBoundaryTests.cs:44-129`, `AtomicWriteBoundaryTests.cs:48-135`, `MapPostAllowlistTests.cs:18-227` | Katman, modül, Reporting izolasyonu, idempotency/allowlist ve write-participant kuralları otomatik test ediliyor. | Mimari sapmaların erken yakalanması; "dokümanda var, kodda yok" riskinin azalması. | Yeni modül/endpoint eklendikçe test kapsamını güncel tutmak. | Hayır (iyi uygulama) |
| G-2 | Bilgi | `src/BuildingBlocks/FinTrack.ModuleContracts/*.cs`, `src/Modules/*/Application/*Registration.cs` | Modüller arası okuma/yazma açık sözleşmeler ve DI kayıtlarıyla yönetiliyor. | Bağımlılık yönü görünür; genişletme noktaları nispeten belirgin. | Sözleşme evriminde geriye dönük uyumluluk/versiyonlama disiplinini sürdürmek. | Hayır |
| G-3 | Bilgi | `src/Host/FinTrack.Api/Idempotency/*`, `WriteTransactions/SqlWriteTransactions.cs`, `Program.cs:59-76,173-176` | Host-owned idempotency, tek bağlantı/aktif scope ve modül katılımı uygulanmış. | Çift yazma ve belirsiz ağ tekrarlarında tutarlılık için sağlam temel. | Sınırları (2xx-only replay, 64KB gövde/yanıt) operasyonel olarak izlemek. | Hayır |
| G-4 | Bilgi | `src/BuildingBlocks/FinTrack.SharedKernel/Money.cs`, `Result.cs`, `RowVersionCodec.cs`, `UserTime.cs`; `src/Modules/*/Domain/*.cs` | Para değeri `decimal`/TRY odaklı, Result tabanlı hata akışı, satır sürümü ve yerel zaman yardımcıları; domain kuralları aggregate içinde yoğun. | Finansal doğruluk ve tutarlılık için iyi temel; anemic model riski kısmen dengelenmiş. | Yinelenen doğrulama/haritalama kalıplarını kontrollü sadeleştirmek. | Hayır |
| G-5 | Bilgi | `src/Mobile/FinTrack.Mobile.Core/PendingOperations/*`, `Api/AuthHandler.cs:14-160`, `PendingOperationRecoveryService.cs` | Şifreli yerel kuyruk, Created/SentUnconfirmed ayrımı, retry/backoff ve durum kontrolü tasarlanmış. | Ağ kesintisi/belirsizlik senaryoları için mimari hazırlık mevcut. | Gerçek yazma akışlarıyla uçtan uca entegrasyonu tamamlamak (bkz. Y-1). | Hayır |
| G-6 | Bilgi | `tests/` envanteri (API integration idempotency/concurrency senaryoları, modül domain+application testleri, mobile testleri) | Çok katmanlı test portföyü ve mimari sınır testleri mevcut. | Regresyon ve sözleşme ihlallerine karşı koruma. | Boşlukları (reporting, uçtan uca mobil akış) hedefli kapatmak. | Hayır |

---

## 4. Kritik ve yüksek riskler

| Önem | Dosya yolu / satır | Mevcut davranış | Oluşturduğu risk | Önerilen çözüm | Zorunlu mu? |
|---|---|---|---|---|---|
| **Yüksek** | `src/Mobile/FinTrack.Mobile.Core/ViewModels/AddExpenseViewModel.cs:80-106`, `src/Mobile/FinTrack.Mobile.Core/Api/FinTrackApiClient.cs:76-79`, karşılaştırma: `PendingOperations/PendingOperationRecoveryService.cs:48-108`, `Api/AuthHandler.cs:41-56,149-159` | `AddExpenseViewModel.SaveAsync` harcamayı doğrudan `CreateExpenseAsync` ile gönderiyor; typed client `Idempotency-Key` eklemiyor ve pending-operation kaydı oluşturmuyor. Pending store/recovery ve AuthHandler altyapısı mevcut ancak bu yazma akışıyla entegre görünmüyor. | Ağ kesintisi/zaman aşımı/401 sonrası senaryolarda kullanıcı aynı işlemi yeniden tetikleyebilir; "tek finansal kayıt" güvencesi istemci akışında tam kullanılmamış olur, belirsiz sonuç riski artar. | Mutasyon POST akışlarını (öncelik: harcama oluşturma gibi kullanıcı tarafından tetiklenenler) `PendingOperationRecoveryService/Create+Send` ve tutarlı `Idempotency-Key` üretimiyle entegre etmek; UI durumlarını (Created/SentUnconfirmed/Uncertain) kullanıcıya açık göstermek. **Mimariyi değiştirmeden** mevcut altyapının tüketimiyle yapılabilir. | **Evet (özellik güvenilirliği/idempotency hedefi için)** |
| **Yüksek** | `src/Modules/Expenses/FinTrack.Modules.Expenses.Application/Reporting/ExpenseReportQuery.cs:19-108`, `src/Modules/CreditCards/FinTrack.Modules.CreditCards.Application/Reporting/CardReportQuery.cs:23-95`, `src/Modules/Debts/FinTrack.Modules.Debts.Application/Reporting/DebtReportQuery.cs:18-126` | Rapor sorguları tüm ilgili kayıtları sayfa sayfa çekip uygulama belleğinde birleştiriyor; örn. `ExpenseReportQuery` tüm aktif harcamaları listeliyor ve her harcama için iadeleri ayrı sorguluyor (`:29-35`, `:79-106`). Kart borç/ekstre ve borç listeleri de benzer şekilde kart/borç başına ek sorgularla hesaplanıyor. | Veri hacmi büyüdüğünde rapor yanıt süreleri, DB yükü ve bellek kullanımı hızla artar (N+1 / tam tarama eğilimi). Mevcut ölçekte kabul edilebilir olabilir; büyümede darboğaz oluşturur. | Okuma modellerini sahip modül içinde agregasyon/projeksiyon odaklı sorgulara taşımak (mevcut sözleşme imzalarını koruyarak implementasyon iyileştirmesi), gerekli indeksleri gözden geçirmek ve rapor endpointleri için performans bütçesi/testi eklemek. | **Hayır (şu an), ancak ölçeklenmeden önce ele alınmalı** |
| **Yüksek** | `src/Modules/CashInflows/FinTrack.Modules.CashInflows.Infrastructure/Persistence/UnitOfWork.cs` (yalnızca `IUnitOfWork`+participant), `src/Modules/Debts/FinTrack.Modules.Debts.Infrastructure/Persistence/UnitOfWork.cs`; karşılaştırma: `Expenses.Infrastructure/Persistence/UnitOfWork.cs:51-87`, `CreditCards.Infrastructure/Persistence/UnitOfWork.cs:51-87`, `tests/FinTrack.ArchitectureTests/AtomicWriteBoundaryTests.cs:112-135` | Expenses/CreditCards modülleri yerel `ITransactionScopeFactory` ve host-scope varken no-op dalı sağlıyor; CashInflows/Debts altyapısında aynı fabrika/desen görünmüyor. Mimari test bu kontrolü yalnızca Expenses/CreditCards için doğruluyor. | Idempotency middleware kapsamındaki POST'larda host transaction baskın olsa da, kapsam dışı/gelecekteki kullanım yollarında modül bazlı transaction davranışı tutarsız olabilir; bakım ve akıl yürütme maliyeti artar. | Modüller arası transaction katılım desenini standartlaştırmak (tüm yazma modülleri için aynı sözleşme/katılım yaklaşımı) ve mimari testi tüm ilgili modüllere genişletmek. | **Hayır**, fakat yeni yazma senaryolarından önce tutarlılık önerilir |

---

## 5. Orta ve düşük öncelikli teknik borçlar

| Önem | Dosya yolu / satır | Mevcut davranış | Oluşturduğu risk | Önerilen çözüm | Zorunlu mu? |
|---|---|---|---|---|---|
| **Orta** | `src/BuildingBlocks/FinTrack.SharedKernel/AggregateRoot.cs:9-15` (domain event koleksiyonu), kod tabanında dispatcher/outbox araması (kanıt: ilgili altyapı dosyası bulunamadı) | Aggregate kökleri domain event taşıyabiliyor; ancak publish/outbox/handler altyapısı görünmüyor. | Event-driven genişlemeler (bildirim, entegrasyon, yan etkiler) için ek tasarım gerekecek; mevcut koleksiyon kullanılmıyorsa ölü soyutlama algısı oluşabilir. | Gerçek ihtiyaç doğduğunda outbox + dispatcher eklemek; aksi halde kapsamı dokümante etmek. | Hayır |
| **Orta** | `src/Modules/Expenses/FinTrack.Modules.Expenses.Endpoints/ExpenseEndpoints.cs:15-186` ve diğer endpoint dosyaları (modüllerde benzer `RequestParsing`/`Problems` kalıpları) | Doğrulama, parse ve ProblemDetails eşleme mantığı endpoint/yardımcı sınıflarda modül bazında tekrarlanıyor. | Yeni endpoint eklerken tutarsız hata biçimi/validasyon riski ve bakım yükü. | Ortak parse/validation helper'larını veya endpoint filtrelerini (sınırları bozmadan) kademeli standardize etmek. | Hayır |
| **Orta** | `src/Host/FinTrack.Api/Identity/AuthEndpoints.cs:54-113`, `src/Host/FinTrack.Api/Program.cs:149-166` | Kayıt akışı explicit transaction kullanıyor; migration'lar modül bazında sırayla uygulanıyor. Kimlik ve iş modülleri ayrı DbContext'lerde. | Çoğu senaryoda yeterli; ancak çapraz context atomiklik beklentisi oluşursa yanlış varsayıma yol açabilir. | Mevcut tasarımı (context sınırlarını) dokümante tutmak; çapraz context senaryosu gerektiren özelliklerde açık tasarım kararı almak. | Hayır |
| **Orta** | `src/Mobile/FinTrack.Mobile.Core/Api/FinTrackApiClient.cs:76-83,129-137` | Create/PUT çağrıları genel POST helper'ı üzerinden, idempotency/pending-operation bağlamı olmadan yapılabiliyor. | Y-1 ile ilişkili; altyapı ile kullanım arasında boşluk. | Yazma operasyonları için "idempotent command send" yardımcısı tanımlayıp ViewModel'leri ona yönlendirmek. | Hayır (Y-1 ile birlikte değerlendirilmeli) |
| **Düşük** | `src/Modules/CashInflows/FinTrack.Modules.CashInflows.Infrastructure/FinTrack.Modules.CashInflows.Infrastructure.csproj` (`InternalsVisibleTo` CreditCards/Debts testlerine) | Testler bazı iç tiplere erişebiliyor. | Test kolaylığı sağlar; modül içi kapsülleme gevşer. | Gerekmedikçe daraltmak veya test dostu public sözleşmelerle ikame etmek. | Hayır |
| **Düşük** | `tests/FinTrack.Modules.Reporting.Tests/FinTrack.Modules.Reporting.Tests.csproj` (yalnızca Reporting.Application referansı), `tests/FinTrack.ArchitectureTests/FinTrack.ArchitectureTests.csproj` (geniş referans seti) | Reporting test projesi dar; mimari test projesi birçok derlemeye bağlı. | Reporting davranışının uçtan uca sözleşme/endpoint bağlamında regresyon görünürlüğü sınırlı olabilir; mimari test derleme süresi artabilir. | Reporting için sözleşme/handler entegrasyon senaryolarını ihtiyaç oldukça genişletmek; mimari test bağımlılıklarını amaçla sınırlı tutmak. | Hayır |

---

## 6. Modül bağımsızlığı değerlendirmesi

| Modül | Kendi iş kuralları/veri sahipliği (kanıt) | Başka modül tablo/DbContext erişimi | Başka modül App/Infra bağımlılığı | Sözleşme üzerinden iletişim | Yeni özellikte tipik etki |
|---|---|---|---|---|---|
| **Expenses** | Harcama/iade/kategori domain kuralları ve `ExpensesDbContext` + `expenses` şeması/migration'ları (`Domain/Expense.cs`, `Infrastructure/ExpensesDbContext*.cs`, `Migrations/*`, `Persistence/*`, `Repositories/*`). | Doğrudan erişim tespit edilmedi; mimari testler bunu koruyor (`LayerBoundaryTests.cs`). | Yok (proje/test sınırları). | Kart doğrulaması için `ICardQuery` tüketimi; Expenses sahipliğinde `ICardExpenseQuery` ve `IExpenseReportQuery` implementasyonları (`Application/Expenses/ExpenseReferenceService.cs`/handler kullanımı bağlamı, `Infrastructure/Queries/CardExpenseQuery.cs`, `Application/Reporting/ExpenseReportQuery.cs`). | Harcama davranışı değişirse çoğunlukla Expenses + ilgili endpoint/UI/test; rapor etkisi sözleşme/implementasyon üzerinden sınırlı kalabilir. |
| **CashInflows** | Para girişi kuralları ve `CashInflowsDbContext` (`Domain/CashInflow.cs`, `Infrastructure/*`). | Doğrudan erişim tespit edilmedi. | Yok. | Borç modüllerine yazma hizmeti `IDebtCashInflowWriter` (`Application/CashInflows/DebtCashInflowWriter.cs`), raporlama için `ICashInflowReportQuery` (`Application/Reporting/CashInflowReportQuery.cs` kayıtlı). | Giriş türü/kural değişimi CashInflows odaklı; borç akışları sözleşme sözleşmesine duyarlı. |
| **CreditCards** | Kart, ödeme, ekstre, nakit avans domain ve `CreditCardsDbContext` (`Domain/CreditCard.cs` vb., `Infrastructure/*`). | Doğrudan erişim yok; harcama toplamları `ICardExpenseQuery` ile Expenses'tan okunur (`Application/Cards/CardDebtService.cs` bağımlılığı/`Application/Reporting/CardReportQuery.cs:17-43`). | Yok. | Nakit avans akışında `IDebtCashInflowWriter` tüketimi (`Application/CashAdvances/CashAdvanceHandlers.cs:15-107`), `ICardQuery`/`ICardReportQuery` sağlanması. | Kart/ekstre/avans değişiklikleri CreditCards + (gerekirse) Expenses sözleşmesi; çapraz yazma `DebtCashInflowWriter` ile kontrollü. |
| **Debts** | Kredi/kişisel borç/taksit/geri ödeme domain ve `DebtsDbContext` (`Domain/Loan.cs` vb., `Infrastructure/*`). | Doğrudan erişim tespit edilmedi. | Yok. | `IDebtCashInflowWriter` ile bağlı para girişi oluşturma/iptal (`Application/Loans/LoanHandlers.cs:13-184` ve kişisel borç handler'ları benzer desen), `IDebtReportQuery`/`ILoanDebtQuery` sağlanması. | Borç ürünü değişimi Debts odaklı; cash inflow tarafı sözleşmeye bağlı. |
| **Reporting** | Kendi kalıcı verisi yok; yalnızca rapor DTO/handler'ları (`Application/Reporting/*`, `Endpoints/*`). | Yok (testle korunuyor `LayerBoundaryTests.cs:95-129`). | Yok. | Tüm veri modül sözleşmeleri üzerinden (`Program.cs` modül kayıtlarıyla sağlanan implementasyonlar). | Yeni rapor çoğu durumda Reporting + ilgili modül(ler)in query sözleşmesi/implementasyonu; modül iç tablolarına erişim gerekmez. |

**Değerlendirme:** Modüller arası **derleme düzeyinde** bağımsızlık güçlüdür. **Davranışsal/veri akışı** bağımlılıkları bilinçli sözleşmelerle yönetilmektedir (özellikle kart borcu için Expenses verisi, borç kaynaklı para girişleri için CashInflows yazma sözleşmesi). Bu, "tamamen bağımsız" değil ama kontrollü ve izlenebilir bir bağımlılıktır.

---

## 7. Beş genişletilebilirlik senaryosunun analizi

> Not: Varsayımsal dosya/isim üretilmemiştir; etki alanları mevcut kod yapısına göre belirtilmiştir.

### 7.1 Bütçe yönetimi
- **Yeni modül mü / mevcut modülde mi:** Muhtemelen **yeni modül** (örn. bütçe tanımı, limit, dönem takibi) daha uygun; harcama/kategori verisi Expenses'tan okunur.
- **Değişmesi muhtemel gerçek alanlar:** `FinTrack.slnx`, `src/Host/FinTrack.Api/Program.cs` (modül DI/map/migration sırası), `src/Host/FinTrack.Api/FinTrack.Api.csproj` (yeni Endpoint/Infra ref), yeni `src/Modules/Budgeting/*` (veya seçilen adla) katman projeleri, `src/BuildingBlocks/FinTrack.ModuleContracts/*` (gerekirse bütçe/harcama özeti okuma sözleşmeleri), `src/Modules/Expenses/Application/Reporting/*` veya ek sözleşme implementasyonları (kategori/dönem harcama toplamları), `src/Modules/Reporting/*` (bütçe raporu eklenirse), `src/Mobile/FinTrack.Mobile.Core/Api/Dtos.cs` + `FinTrackApiClient.cs` + yeni ViewModel/View (`ViewModels/*`, `Views/*`, `MauiProgram.cs`, `AppShell.xaml.cs`), test projeleri ve mimari test kapsamı.
- **İletişim kurulacak modüller:** Expenses (harcama/kategori), muhtemelen Reporting; para girişiyle karşılaştırma istenirse CashInflows.
- **Yeni sözleşmeler:** Dönemsel harcama/bütçe karşılaştırması için okuma sözleşmesi gerekebilir (`IExpenseReportQuery` mevcut ama bütçe semantiği farklı olabilir).
- **Veri sahipliği korunabilir mi:** Evet, bütçe tanımları ayrı şemada tutulabilir; Expenses verisinin sahibi değişmez.
- **Zorlaştıran mevcut kararlar:** Rapor/özet sorgularının mevcut in-memory sayfalama yaklaşımı (`ExpenseReportQuery.cs:19-108`) bütçe karşılaştırması eklenince maliyeti artırabilir.
- **Tahmini değişiklik yayılımı:** **Orta–Yüksek** (yeni uçtan uca dikey dilim: backend modülü + sözleşme + mobil + testler).

### 7.2 Tekrarlayan harcamalar
- **Yeni modül mü / mevcut modülde mi:** **Expenses içinde genişleme** (şablon/plan) + **zamanlanmış yürütme** için host/arka plan altyapısı gerektirir.
- **Değişmesi muhtemel gerçek alanlar:** `src/Modules/Expenses/Domain/*` (yeni aggregate/kurallar), `Application/*` (command/handler), `Infrastructure/*` (DbContext/config/migration, repository), `Endpoints/*`, `src/Host/FinTrack.Api/Program.cs` (background service/hosted service kaydı gerekiyorsa), mobil `Api/Dtos.cs`/`FinTrackApiClient.cs`/ilgili ViewModel-View'lar, testler.
- **İletişim kurulacak modüller:** Öncelikle Expenses; kartla ilişkiliyse CreditCards kart doğrulaması (`ICardQuery`) dolaylı olarak devrede.
- **Yeni sözleşmeler:** Çoğu durumda gerekmez; sadece Expenses iç modeli yeterli olabilir.
- **Veri sahipliği korunabilir mi:** Evet.
- **Zorlaştıran mevcut kararlar:** Şu an kod tabanında zamanlayıcı/arka plan iş altyapısı görünmüyor (Program.cs'de hosted recurring worker yok); idempotency mutasyon POST akışına bağlı, arka plan üretimi için ayrı tetikleme/idempotency stratejisi tasarlanmalı.
- **Tahmini değişiklik yayılımı:** **Orta** (Expenses içi) + **orta** (zamanlama/operasyonel güvenilirlik) = toplam **Orta–Yüksek**.

### 7.3 Bildirim ve hatırlatmalar
- **Yeni modül mü / mevcut modülde mi:** Genellikle **yeni modül/altyapı bileşeni** (bildirim tercihleri, teslimat, zamanlama) + diğer modüllerden olay/veri okuma.
- **Değişmesi muhtemel gerçek alanlar:** Yeni modül projeleri ve host kaydı (`Program.cs`, `FinTrack.Api.csproj`, `FinTrack.slnx`), `src/BuildingBlocks/FinTrack.ModuleContracts/*` (ör. yaklaşan ödeme/ekstre kaynaklı okuma sözleşmeleri mevcutsa genişletme; `ICardReportQuery.GetStatementDuesAsync`, `IDebtReportQuery.GetUpcomingInstallmentsAsync` hâlihazırda var: `ReportingReportContracts.cs:79-109`), `src/Modules/Reporting/Application/Reporting/ReportHandlers.cs` (mantık yeniden kullanım ihtiyacına göre), mobil push/local notification entegrasyonu (`src/Mobile/FinTrack.Mobile/*`, platform dosyaları), dış servis entegrasyonu (e-posta/push) için host konfigürasyonu.
- **İletişim kurulacak modüller:** CreditCards, Debts (vadeler), muhtemelen Expenses/CashInflows (eşik/bütçe senaryolarına bağlı).
- **Yeni sözleşmeler:** Kullanıcı tercihleri/kanal yönetimi ve tetikleyici veriler için yeni sözleşmeler gerekebilir.
- **Veri sahipliği korunabilir mi:** Evet, bildirim durumu/tercihleri ayrı sahiplikte tutulabilir.
- **Zorlaştıran mevcut kararlar:** Domain event/outbox altyapısı görünmüyor (`SharedKernel/AggregateRoot.cs`); zamanlama ve teslimat güvenilirliği yeni çapraz kesen gerektirir.
- **Tahmini değişiklik yayılımı:** **Yüksek** (yeni altyapı + mobil/platform + operasyon).

### 7.4 Çoklu para birimi
- **Yeni modül mü / mevcut modülde mi:** **Çapraz kesen değişiklik**; tek bir modülle sınırlı değil.
- **Değişmesi muhtemel gerçek alanlar:** `src/BuildingBlocks/FinTrack.SharedKernel/Money.cs` (model uygun olsa da kullanım varsayımları), çok sayıda domain doğrulaması/normalizasyon (`Modules/*/Domain/*.cs`, ör. `Expenses.Domain/Expense.cs:301-305`, `CashInflows.Domain/CashInflow.cs:277-283`, `Debts.Domain/*` doğrulamaları), application/endpoint DTO ve request parse (`Modules/*/Application/*`, `Modules/*/Endpoints/Requests.cs`), raporlama/toplama mantığı ve sözleşmeler (`ModuleContracts/Reporting*.cs`, `Reporting.Application/Reporting/ReportHandlers.cs:59-167`, modül rapor query'leri), EF konfigürasyonları ve saklama formatı (`Modules/*/Infrastructure/Configurations/*`), mobil formatlama ve API modelleri (`Mobile.Core/Formatting/MoneyFormat.cs`, `Api/Dtos.cs`, ViewModel'ler), testlerin geniş bölümü.
- **İletişim kurulacak modüller:** Tümü (Expenses, CashInflows, CreditCards, Debts, Reporting) + mobil.
- **Yeni sözleşmeler:** Kur/fx kaynağı, baz para birimi, dönüşüm zamanı/as-of kuralları için yeni sözleşme ve muhtemelen yeni bileşen gerekir.
- **Veri sahipliği korunabilir mi:** İşlem para birimi sahip modülde kalabilir; ancak raporlama/karşılaştırma için ortak kurallar gerekir.
- **Zorlaştıran mevcut kararlar:** Raporlar ve özetler tek `Money.DefaultCurrency` (`TRY`) varsayımıyla üretiliyor (`Reporting.Application/Reporting/ReportHandlers.cs:61,95,104` vb.); domain'lerde TRY dışı para birimi reddi yaygın; mevcut toplamlar aynı para birimi toplama varsayımına dayanıyor.
- **Tahmini değişiklik yayılımı:** **Çok yüksek** (sistem genelinde kırılım riski yüksek).

### 7.5 Parola sıfırlama ve e-posta doğrulama
- **Yeni modül mü / mevcut modülde mi:** **Host/Identity alanında genişleme** (iş modülü değil); e-posta gönderimi için yeni altyapı bileşeni.
- **Değişmesi muhtemel gerçek alanlar:** `src/Host/FinTrack.Api/Identity/*` (`AuthEndpoints.cs`, `AuthContracts.cs`, `AuthValidation.cs`, `ApplicationUser.cs`, token/identity konfigürasyonu `Program.cs:82-99`), yeni endpoint'ler/akışlar ve token üretim-doğrulama, SMTP/provider entegrasyonu ve konfigürasyon (`appsettings*`, DI), olası migration'lar (identity şeması) — `Identity/Migrations/*`, mobil akışlar (`Mobile.Core/Api/FinTrackApiClient.cs`, `ViewModels/LoginViewModel.cs`, `RegisterViewModel.cs`, yeni ekranlar/viewmodel'ler), entegrasyon testleri (`tests/FinTrack.Api.IntegrationTests/AuthEndpointTests.cs`).
- **İletişim kurulacak modüller:** İş modülleriyle doğrudan veri yazışması gerekmeyebilir; kullanıcı oturumu/kimlik doğrulama ile sınırlı.
- **Yeni sözleşmeler:** Genellikle modül sözleşmesi gerekmez; host API sözleşmesi (endpoint) genişler.
- **Veri sahipliği korunabilir mi:** Evet (Identity host sahipliğinde kalır).
- **Zorlaştıran mevcut kararlar:** `SignIn.RequireConfirmedEmail = false` (`Program.cs:93`) ve e-posta gönderim altyapısı bulunmuyor; güvenli token ömrü, tek kullanımlık token, rate limiting ve kullanıcı deneyimi akışları ek tasarım gerektirir. Idempotency middleware auth rotalarını kapsam dışı bırakıyor (`IdempotencyMiddleware.cs:386-389`) — bu uygun, ancak yeni auth POST'larının tekrar/güvenlik semantiği ayrı ele alınmalı.
- **Tahmini değişiklik yayılımı:** **Orta** (host+mobil), dış bağımlılık ve güvenlik gereksinimlerine göre artabilir.

---

## 8. Test mimarisi değerlendirmesi

| Önem | Dosya yolu / kanıt | Mevcut davranış | Oluşturduğu risk | Önerilen çözüm | Zorunlu mu? |
|---|---|---|---|---|---|
| **Bilgi (Güçlü)** | `tests/FinTrack.ArchitectureTests/*.cs`, `tests/FinTrack.Api.IntegrationTests/Idempotency*.cs`, `AuthEndpointTests.cs`, modül test klasörleri, `tests/FinTrack.Mobile.Tests/*` | Mimari sınırlar, idempotency senaryoları, modül domain/uygulama ve mobil birim/davranış testleri mevcut. | Temel regresyonlara karşı iyi koruma. | Kritik iş kuralları eklendikçe senaryo tabanlı testleri sürdürmek. | Hayır |
| **Orta** | `tests/FinTrack.Modules.Reporting.Tests/FinTrack.Modules.Reporting.Tests.csproj`, `tests/FinTrack.Modules.Reporting.Tests/ReportingHandlerTests.cs` (envanter) | Reporting testleri Application düzeyinde sınırlı görünüyor. | Sözleşme implementasyonları/modül entegrasyonu değişince rapor doğruluğu regresyonları geç fark edilebilir. | Reporting handler'larını modül query sözleşme fake/fixture'larıyla daha kapsamlı senaryolarda (tarih sınırı, iptal, Unknown inflow, kart alacağı vb.) test etmek. | Hayır |
| **Orta** | `src/Mobile/FinTrack.Mobile.Core/ViewModels/AddExpenseViewModel.cs:80-106` ile `tests/FinTrack.Mobile.Tests/*PendingOperation*` karşılaştırması | Pending-operation/recovery bileşenleri test ediliyor; ancak en az bir gerçek yazma ViewModel akışı bu bileşenleri kullanmıyor. | "Test edilen kurtarma altyapısı" ile "kullanıcı akışı" arasında boşluk kalabilir. | ViewModel/API entegrasyonunu idempotent akışla birleştiren testler eklemek. | Hayır (Y-1 ile ilişkili) |
| **Düşük** | Test envanteri (UI otomasyon projesi görülmedi) | Windows UI/manual smoke operatör bağımlı; otomatik UI E2E katmanı görünmüyor. | UI regresyonları manuel doğrulamaya bağlı kalır. | Kritik yol için hedefli otomasyon veya düzenli smoke checklist'i. | Hayır |

**Not:** Tam entegrasyon paketinin bu oturumda yerelde yeniden koşulmadığı (Docker kapalı) denetim bağlamında belirtilmiştir; bu, mevcut testlerin kalitesine dair olumsuz kanıt değildir.

---

## 9. Önceliklendirilmiş iyileştirme önerileri

1. **(Yüksek, zorunlu)** Mobil mutasyon yazma akışlarını (`AddExpenseViewModel` ve benzeri) pending-operation + `Idempotency-Key` akışıyla entegre etmek.\
   *Etkilenen alanlar:* `src/Mobile/FinTrack.Mobile.Core/ViewModels/AddExpenseViewModel.cs`, `Api/FinTrackApiClient.cs`, gerekirse `PendingOperationRecoveryService` kullanım modeli, ilgili mobil testler.
2. **(Yüksek)** Raporlama okuma sorgularının uygulama-içi tam tarama/N+1 eğilimini azaltmak (sözleşmeleri bozmadan).\
   *Etkilenen alanlar:* `Expenses.Application.Reporting.ExpenseReportQuery`, `CreditCards.Application.Reporting.CardReportQuery`, `Debts.Application.Reporting.DebtReportQuery`, ilgili query portları/implementasyonları.
3. **(Yüksek/Orta)** Yazma modülleri arasında transaction katılım ve yerel scope fabrikası desenini tutarlı hale getirmek.\
   *Etkilenen alanlar:* `CashInflows.Infrastructure.Persistence.UnitOfWork.cs`, `Debts.Infrastructure.Persistence.UnitOfWork.cs`, ilgili DI kayıtları ve `AtomicWriteBoundaryTests.cs`.
4. **(Orta)** Endpoint doğrulama/parse/problem eşleme tekrarlarını azaltacak ortak yardımcılar/filtreler.\
   *Etkilenen alanlar:* `src/Modules/*/Endpoints/*`, `RequestParsing.cs`.
5. **(Orta)** Reporting ve mobil idempotent akış kapsama testlerini genişletmek.\
   *Etkilenen alanlar:* `tests/FinTrack.Modules.Reporting.Tests/*`, `tests/FinTrack.Mobile.Tests/*`.
6. **(Düşük/Orta)** Domain event kullanımı için net strateji (outbox gereksinimi doğana kadar dokümantasyon).\
   *Etkilenen alanlar:* `src/BuildingBlocks/FinTrack.SharedKernel/AggregateRoot.cs` ve gelecekteki host/modül altyapısı.

---

## 10. Mimariyi değiştirmeden yeni özellik geliştirilebilecek alanlar

- **Mevcut modüller içinde CRUD/iş kuralı genişletmeleri:** Expenses, CashInflows, CreditCards, Debts modüllerinin kendi bounded context'leri içinde (yeni alanlar, doğrulamalar, listeleme filtreleri) mevcut katman düzeni korunarak geliştirme yapılabilir.\
  *Örnek kanıt yapısı:* `src/Modules/*/Domain/*`, `Application/*Handlers*.cs`, `Infrastructure/*`, `Endpoints/*`.
- **Yeni okuma raporları / mevcut raporların genişletilmesi:** `ModuleContracts` üzerinden veri sağlayarak Reporting'e yeni endpoint/handler eklenmesi, modül sahipliği korunarak mümkündür.\
  *Kanıt:* `src/Modules/Reporting/*`, `src/BuildingBlocks/FinTrack.ModuleContracts/Reporting*.cs`, `Program.cs:146,192`.
- **Mevcut sözleşmelerle yeni tüketiciler:** `ICardExpenseQuery`, `ICardReportQuery`, `IDebtReportQuery` gibi sözleşmeler üzerinden ek okuma senaryoları eklenebilir.
- **Mobilde mevcut altyapının tamamlanması:** Pending-operation ve recovery bileşenleri mevcut; mimari değişikliği gerektirmeden UI akışlarına bağlanabilir.\
  *Kanıt:* `src/Mobile/FinTrack.Mobile.Core/PendingOperations/*`, `MauiProgram.cs:34-65`.
- **Ek modül ekleme (benzer kalıpla):** Mevcut modül şablonuna uygun yeni modül, host kompozisyonuna açık kayıtlarla eklenebilir (sınırlar mimari testlerle korunmalı).\
  *Kanıt:* `Program.cs:140-146`, modül `*Module.cs` dosyaları, `LayerBoundaryTests.cs`.

---

## 11. Yeni özellik geliştirmeden önce çözülmesi gereken problemler

| # | Önem | Problem | Neden önce çözülmeli? | İlgili kanıt |
|---|---|---|---|---|
| P-1 | **Yüksek (zorunlu)** | Mobilde mutasyon POST'larının idempotent/pending-operation akışına tam entegrasyonu (en az kritik oluşturma akışları). | Çift kayıt/belirsiz sonuç kullanıcı deneyimi ve veri güvenilirliği riski taşır; özellikle ağ sorunları olan ortamlarda idempotency hedefi eksik kalır. | `AddExpenseViewModel.cs:80-106`, `FinTrackApiClient.cs:76-79`, `AuthHandler.cs:41-56`, `PendingOperationRecoveryService.cs:48-108` |
| P-2 | **Yüksek (şartlı)** | Raporlama sorgularının performans karakteristiği (tam tarama/N+1) — veri büyümesi veya yeni rapor/bütçe özellikleri öncesi değerlendirme. | Yeni raporlama/bütçe senaryoları mevcut maliyeti katlayabilir. | `ExpenseReportQuery.cs:19-108`, `CardReportQuery.cs:23-95`, `DebtReportQuery.cs:18-126` |
| P-3 | **Orta** | Yazma modüllerinde transaction/participant desen tutarlılığı ve mimari test kapsamının genişletilmesi. | Atomik yazma ve idempotency davranışının modüller arası tutarlı olması bakım/yanlış kullanım riskini azaltır. | `CashInflows/Debts.Infrastructure.Persistence.UnitOfWork.cs`, `AtomicWriteBoundaryTests.cs:112-135` |
| P-4 | **Orta (senaryoya bağlı)** | Çoklu para birimi veya e-posta tabanlı kimlik akışları için ön tasarım (kur modeli, doğrulama token'ları, dış servis, API sözleşmesi). | Bu alanlar çapraz kesen kırılım yaratır; doğrudan kodlamaya başlamak yüksek yeniden iş riski doğurur. | `Money.cs`, TRY doğrulamaları (ör. `Expense.cs:301-305`), `ReportHandlers.cs:61,95`, `AuthEndpoints.cs`, `Program.cs:82-99` |

---

## 12. Son değerlendirme (açık sorular)

### 1) FinTrack'in mimarisi iyi mi?
**Evet, genel olarak iyi ve disiplinli.** Modüler monolit sınırları, sözleşme tabanlı entegrasyon, host-owned idempotency/paylaşılan yazma transaction yaklaşımı ve mimari testler güçlü yönlerdir (`LayerBoundaryTests.cs`, `AtomicWriteBoundaryTests.cs`, `ModuleContracts/*`, `Program.cs`, `IdempotencyMiddleware.cs`). Zayıf yönler daha çok ölçek/performans (raporlama), mobil yazma akışlarının tam entegrasyonu ve bazı desen tutarsızlıklarıdır.

### 2) Yeni modül eklemek kolay mı?
**Görece kolay.** Mevcut kompozisyon modeli (`*Module.Add*/Map*`, `Program.cs:140-146,188-192`), şema/migration ayrımı ve mimari testler yeni modül eklemeyi destekler. Ancak yeni modül; sözleşme tasarımı, endpoint allowlist/idempotency kapsamı, mobil/API yüzeyi ve test güncellemeleri gerektirdiğinden "sıfır efor" değildir.

### 3) Mevcut modüller birbirinden gerçekten bağımsız mı?
**Derleme ve doğrudan veri erişimi düzeyinde büyük ölçüde evet; davranışsal düzeyde kontrollü bağımlılık vardır.** Modüller birbirinin derlemelerine/tablolarına doğrudan bağlı değildir (mimari testlerle korunur). Bununla birlikte Expenses↔CreditCards arasında `ICardExpenseQuery`/`ICardQuery`, borç modülleri↔CashInflows arasında `IDebtCashInflowWriter`, tüm veri modülleri↔Reporting arasında rapor sözleşmeleri vardır. Bu bağımlılıklar açık ve sözleşme temellidir.

### 4) Proje büyüdükçe mimari karmaşıklık kontrol altında kalabilir mi?
**Koşullu evet.** Sınır testleri, açık sözleşmeler ve modül başına sahiplik bunu destekler. Kontrolü zorlaştırabilecek faktörler: raporlama/okuma modellerinin in-memory büyümesi, çapraz kesen gereksinimler (çoklu para birimi, bildirim, zamanlama), endpoint/validasyon tekrarları ve mobilde idempotent akışın tutarlı uygulanmaması. Bu riskler yönetilirse karmaşıklık kontrol edilebilir.

### 5) En fazla dikkat etmemiz gereken üç mimari risk nedir?
1. **Mobil yazma akışlarının idempotency/pending-operation altyapısıyla tam bütünleşmemesi** → çift/belirsiz işlem riski (`AddExpenseViewModel.cs`, `FinTrackApiClient.cs`, pending-op bileşenleri).\
2. **Raporlama sorgularının ölçeklenme davranışı (tam tarama/N+1, bellek içi agregasyon)** → performans ve maliyet riski (`ExpenseReportQuery.cs`, `CardReportQuery.cs`, `DebtReportQuery.cs`).\
3. **Çapraz kesen değişimlerin (özellikle çoklu para birimi) mevcut TRY/tek para birimi varsayımlarına yayılması** → geniş kırılım ve yüksek yeniden iş riski (`Money` kullanımı, domain TRY kontrolleri, raporlama varsayımları).

### 6) Parola sıfırlama ve e-posta doğrulama geliştirmesine mevcut mimariyle başlayabilir miyiz?
**Evet, başlayabiliriz; ancak ön hazırlık koşullarıyla.** Kimlik host içinde izole edildiği için iş modüllerine mimari kırılım gerekmeden Identity/API/mobil akışları genişletilebilir (`src/Host/FinTrack.Api/Identity/*`, `Program.cs:82-99,187`). Başlamadan önce netleştirilmesi gerekenler: e-posta teslim altyapısı ve konfigürasyon, güvenli/tek kullanımlık token yaşam döngüsü, rate limiting/istismar önlemleri, `RequireConfirmedEmail` ve kullanıcı akışı kararları, ve ilgili API/mobil sözleşme güncellemeleri. Bu geliştirme mevcut modül sınırlarını bozmaz; ana etki host identity ve mobil istemcidedir.
