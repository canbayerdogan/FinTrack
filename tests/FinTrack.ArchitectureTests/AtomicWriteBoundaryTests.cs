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
