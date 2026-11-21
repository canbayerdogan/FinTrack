using System.Reflection;
using FinTrack.Modules.CashInflows.Endpoints;
using FinTrack.Modules.CreditCards.Endpoints;
using FinTrack.Modules.Debts.Endpoints;
using FinTrack.Modules.Expenses.Endpoints;
using FinTrack.Modules.Reporting.Endpoints;
using FinTrack.ModuleContracts;
using NetArchTest.Rules;
using Xunit;

namespace FinTrack.ArchitectureTests;

/// <summary>
/// docs/11 §2 / §4.5 / ADR-0010 modül sınırı kuralları. Çoğu doğrulama derleme
/// metadatası üzerindendir (yapısal); MapPost allowlist ve yerel factory guard
/// kontrolleri kaynak metni üzerinden yapılır, çünkü route/guard ifadeleri
/// çalışma zamanında kolayca gözlemlenemez. Bu davranışlar S25 entegrasyon
/// testleri ve WriteTransactionInternalsTests birim testleri ile tamamlanır.
/// </summary>
public sealed class AtomicWriteBoundaryTests
{
    private static readonly (string Name, Assembly Assembly)[] ModuleAssemblies =
    [
        ("Expenses", typeof(ExpensesModule).Assembly),
        ("CashInflows", typeof(CashInflowsModule).Assembly),
        ("CreditCards", typeof(CreditCardsModule).Assembly),
        ("Debts", typeof(DebtsModule).Assembly),
        ("Reporting", typeof(ReportingModule).Assembly),
    ];

    private static readonly (string Name, Assembly Assembly)[] ApplicationAssemblies =
    [
        ("Expenses", typeof(FinTrack.Modules.Expenses.Application.ApplicationAssembly).Assembly),
        ("CashInflows", typeof(FinTrack.Modules.CashInflows.Application.ApplicationAssembly).Assembly),
        ("CreditCards", typeof(FinTrack.Modules.CreditCards.Application.ApplicationAssembly).Assembly),
        ("Debts", typeof(FinTrack.Modules.Debts.Application.ApplicationAssembly).Assembly),
        ("Reporting", typeof(FinTrack.Modules.Reporting.Application.ReportingApplicationAssembly).Assembly),
    ];

    private static readonly (string Name, string AssemblyName)[] WriteModules =
    [
        ("Expenses", "FinTrack.Modules.Expenses.Infrastructure"),
        ("CashInflows", "FinTrack.Modules.CashInflows.Infrastructure"),
        ("CreditCards", "FinTrack.Modules.CreditCards.Infrastructure"),
        ("Debts", "FinTrack.Modules.Debts.Infrastructure"),
    ];

    [Fact]
    public void Hicbir_modul_host_idempotency_derlemesine_bagimli_olmamali()
    {
        foreach (var (name, assembly) in ModuleAssemblies)
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name == "FinTrack.Api");

            Assert.DoesNotContain(
                assembly.GetTypes(),
                type => type.FullName?.StartsWith("FinTrack.Api.", StringComparison.Ordinal) == true);
        }

        var result = Types.InAssemblies(ModuleAssemblies.Select(entry => entry.Assembly).ToArray())
            .ShouldNot()
            .HaveDependencyOnAny("FinTrack.Api", "FinTrack.Api.Idempotency")
            .GetResult();
        AssertSuccess(result, "Modüller host/Idempotency tipine bağımlı olmamalı");
    }

    [Fact]
    public void Application_katmanlari_EF_ve_SqlClient_e_bagimli_olmamali()
    {
        foreach (var (name, assembly) in ApplicationAssemblies)
        {
            var references = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
            Assert.DoesNotContain("Microsoft.Data.SqlClient", references);

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient")
                .GetResult();
            AssertSuccess(result, $"{name}.Application altyapı veri erişimine bağımlı olmamalı");
        }
    }

    [Fact]
    public void Butun_yazma_modullerinin_UnitOfWork_tipleri_IWriteScopeParticipant_uygulamali()
    {
        foreach (var (name, assemblyName) in WriteModules)
        {
            var assembly = Assembly.Load(assemblyName);
            var unitOfWorks = assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.Name == "UnitOfWork")
                .ToArray();

            Assert.NotEmpty(unitOfWorks); // tip bulunamazsa test boşa geçmesin
            foreach (var unitOfWork in unitOfWorks)
            {
                Assert.True(
                    typeof(IWriteScopeParticipant).IsAssignableFrom(unitOfWork),
                    $"{assemblyName}.{unitOfWork.Name} IWriteScopeParticipant uygulamalı (ADR-0010).");
                Assert.Contains(unitOfWork.GetInterfaces(), iface => iface.Name == "IUnitOfWork");
            }
        }
    }

    [Theory]
    [InlineData("Expenses")]
    [InlineData("CreditCards")]
    public void Yerel_transaction_fabricasi_host_scope_varken_no_op_dallaniyor(string module)
    {
        // Yapısal (kaynak metni) doğrulama: TransactionScopeFactory internal ve
        // davranışı DbContext gerektiriyor; no-op davranışı S25 entegrasyon
        // testlerinde gözlemlenir. Burada ADR-0010 kural 3'ün kaynakta varlığı
        // ve host scope'una göre dallandığı doğrulanır.
        var root = FindRepoRoot();
        var file = Path.Combine(
            root, "src", "Modules", module,
            $"FinTrack.Modules.{module}.Infrastructure", "Persistence", "UnitOfWork.cs");
        Assert.True(File.Exists(file), $"Kaynak dosya bulunamadı: {file}");

        var text = File.ReadAllText(file);
        var start = text.IndexOf("class TransactionScopeFactory", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{file} içinde TransactionScopeFactory bulunamadı.");

        var next = text.IndexOf("internal sealed class", start + 1, StringComparison.Ordinal);
        var body = next < 0 ? text[start..] : text[start..next];

        Assert.Contains("BeginSerializableAsync", body);
        Assert.Contains("scopeAccessor?.Current", body);
        Assert.Contains("HostTransactionScope", body);
    }

    [Fact]
    public void Transaction_sozlesmeleri_buildingblocks_ta_ve_dort_modulde_baglanti_var()
    {
        var contracts = typeof(IWriteScopeParticipant).Assembly;
        Assert.Equal("FinTrack.ModuleContracts", contracts.GetName().Name);

        foreach (var contract in new[]
        {
            typeof(IWriteConnectionProvider),
            typeof(IWriteTransactionScope),
            typeof(IWriteTransactionScopeFactory),
            typeof(IWriteTransactionScopeAccessor),
            typeof(IWriteScopeParticipant),
            typeof(IDebtCashInflowWriter),
        })
        {
            Assert.True(
                ReferenceEquals(contracts, contract.Assembly),
                $"{contract.Name} BuildingBlocks contract'ı olmalı.");
        }

        foreach (var (name, assemblyName) in WriteModules)
        {
            var assembly = Assembly.Load(assemblyName);
            Assert.True(
                assembly.GetReferencedAssemblies().Any(r => r.Name == "FinTrack.ModuleContracts"),
                $"{name}.Infrastructure BuildingBlocks transaction contract'larına bağlı olmalı.");
        }
    }

    [Fact]
    public void UnitOfWork_Enlist_imzasi_ve_host_fallback_kaynakta_korunuyor()
    {
        var root = FindRepoRoot();

        foreach (var (name, assemblyName) in WriteModules)
        {
            var assembly = Assembly.Load(assemblyName);
            var unitOfWork = assembly.GetTypes()
                .Single(type => type.IsClass && !type.IsAbstract && type.Name == "UnitOfWork");

            var enlist = unitOfWork.GetMethod("Enlist", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(enlist);
            Assert.Equal(typeof(void), enlist!.ReturnType);

            var parameters = enlist.GetParameters();
            Assert.Single(parameters);
            Assert.Equal(typeof(IWriteTransactionScope), parameters[0].ParameterType);

            var file = Path.Combine(
                root, "src", "Modules", name,
                $"FinTrack.Modules.{name}.Infrastructure", "Persistence", "UnitOfWork.cs");
            Assert.True(File.Exists(file), $"Kaynak dosya bulunamadı: {file}");
            var text = File.ReadAllText(file);

            Assert.Contains("scopeAccessor?.Current", text); // Enlist(null) host fallback'i
            Assert.Contains("UseTransaction", text);          // participant katılım mekanizması
        }
    }

    [Fact]
    public void Moduller_ham_SqlClient_bagintisi_veya_transaction_acmasi_yapmiyor()
    {
        var root = FindRepoRoot();

        foreach (var (name, _) in WriteModules)
        {
            var moduleRoot = Path.Combine(root, "src", "Modules", name);
            var files = Directory.GetFiles(moduleRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                               !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToArray();
            Assert.NotEmpty(files);

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                foreach (var forbidden in new[] { "new SqlConnection", "SqlConnection(", "new SqlTransaction", "SqlTransaction(" })
                {
                    Assert.False(
                        text.Contains(forbidden, StringComparison.Ordinal),
                        $"{name} modülü host koordinasyonunu ham SqlClient ile atlamamalı: {forbidden} ({file})");
                }
            }
        }
    }

    [Theory]
    [InlineData("Expenses", true)]
    [InlineData("CreditCards", true)]
    [InlineData("CashInflows", false)]
    [InlineData("Debts", false)]
    public void Transaction_baslatma_sadece_mevcut_yerel_fabrikada(string module, bool yerelFabrikaVar)
    {
        var root = FindRepoRoot();
        var moduleRoot = Path.Combine(root, "src", "Modules", module);
        var starting = Directory.GetFiles(moduleRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("BeginTransaction", StringComparison.Ordinal))
            .ToArray();

        if (!yerelFabrikaVar)
        {
            // CashInflows/Debts: bağımsız transaction başlatma hakkı yok; mevcut tasarımda yok.
            Assert.Empty(starting);
            return;
        }

        // Expenses/CreditCards: ADR-0009 yerel fabrikası korunur; ihlal sayılmaz.
        // Tek izinli nokta UnitOfWork.cs içindeki TransactionScopeFactory'dir.
        var unitOfWorkFile = Path.Combine(
            moduleRoot, $"FinTrack.Modules.{module}.Infrastructure", "Persistence", "UnitOfWork.cs");
        Assert.Single(starting);
        Assert.Equal(unitOfWorkFile, starting[0]);

        var text = File.ReadAllText(unitOfWorkFile);
        var start = text.IndexOf("class TransactionScopeFactory", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var next = text.IndexOf("internal sealed class", start + 1, StringComparison.Ordinal);
        var factoryBody = next < 0 ? text[start..] : text[start..next];

        Assert.Contains("BeginTransactionAsync", factoryBody);
        Assert.Contains("scopeAccessor?.Current", factoryBody); // host scope varsa açılmaz
    }

    private static void AssertSuccess(TestResult result, string scenario)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var failing = result.FailingTypeNames is null
            ? "(ihlal eden tip bildirilmedi)"
            : string.Join(", ", result.FailingTypeNames.Take(10));
        Assert.Fail($"{scenario}. İhlal eden tipler: {failing}");
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Modules")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repo kökü (src/Modules + tests) bulunamadı.");
    }
}
