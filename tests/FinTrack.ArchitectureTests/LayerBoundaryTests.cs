using System.Reflection;
using FinTrack.Modules.CashInflows.Endpoints;
using FinTrack.Modules.CreditCards.Endpoints;
using FinTrack.Modules.Debts.Endpoints;
using FinTrack.Modules.Expenses.Endpoints;
using FinTrack.Modules.Reporting.Endpoints;
using NetArchTest.Rules;
using Xunit;

namespace FinTrack.ArchitectureTests;

/// <summary>
/// Modül ve katman sınırlarını derleyicinin yakalayamadığı yerlerde test eder.
/// Bir sınır ihlali burada kırmızı olmalıdır; aksi halde mimari belgede yazılı kalır.
/// </summary>
public sealed class LayerBoundaryTests
{
    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(FinTrack.Modules.Expenses.Domain.ExpensesDomainAssembly).Assembly,
        typeof(FinTrack.Modules.CashInflows.Domain.CashInflowsDomainAssembly).Assembly,
        typeof(FinTrack.Modules.CreditCards.Domain.CreditCardsDomainAssembly).Assembly,
        typeof(FinTrack.Modules.Debts.Domain.DebtsDomainAssembly).Assembly,
    ];

    private static readonly (string Name, Assembly Assembly)[] ApplicationAssemblies =
    [
        ("Expenses", typeof(FinTrack.Modules.Expenses.Application.ApplicationAssembly).Assembly),
        ("CashInflows", typeof(FinTrack.Modules.CashInflows.Application.ApplicationAssembly).Assembly),
        ("CreditCards", typeof(FinTrack.Modules.CreditCards.Application.ApplicationAssembly).Assembly),
        ("Debts", typeof(FinTrack.Modules.Debts.Application.ApplicationAssembly).Assembly),
        ("Reporting", typeof(FinTrack.Modules.Reporting.Application.ReportingApplicationAssembly).Assembly),
    ];

    private static readonly (string Name, Assembly Assembly)[] ModuleAssemblies =
    [
        ("Expenses", typeof(ExpensesModule).Assembly),
        ("CashInflows", typeof(CashInflowsModule).Assembly),
        ("CreditCards", typeof(CreditCardsModule).Assembly),
        ("Debts", typeof(DebtsModule).Assembly),
        ("Reporting", typeof(ReportingModule).Assembly),
    ];

    [Fact]
    public void Domain_katmani_altyapiya_bagimli_olmamali()
    {
        var result = Types.InAssemblies(DomainAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions.DependencyInjection",
                "System.Net.Http",
                "System.Data")
            .GetResult();

        AssertSuccess(result, "Domain katmanı altyapıya bağımlı olmamalı");
    }

    [Fact]
    public void Application_katmani_kendi_infrastructure_una_bagimli_olmamali()
    {
        foreach (var (name, assembly) in ApplicationAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    $"FinTrack.Modules.{name}.Infrastructure",
                    "Microsoft.EntityFrameworkCore")
                .GetResult();

            AssertSuccess(result, $"{name}.Application katmanı Infrastructure'a bağımlı olmamalı");
        }
    }

    [Fact]
    public void Moduller_birbirinin_derlemesine_bagimli_olmamali()
    {
        foreach (var (name, assembly) in ModuleAssemblies)
        {
            var forbidden = ModuleAssemblies
                .Where(m => m.Name != name)
                .Select(m => $"FinTrack.Modules.{m.Name}")
                .ToArray();

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            AssertSuccess(result, $"{name} modülü başka bir modüle bağımlı olmamalı");
        }
    }

    [Fact]
    public void Reporting_baska_modulun_tablosuna_dogrudan_erisemez()
    {
        var reportingApplication = ApplicationAssemblies
            .Single(entry => entry.Name == "Reporting").Assembly;
        var reportingEndpoints = typeof(ReportingModule).Assembly;

        var forbidden = ModuleAssemblies
            .Where(entry => entry.Name != "Reporting")
            .SelectMany(entry => new[]
            {
                $"FinTrack.Modules.{entry.Name}.Domain",
                $"FinTrack.Modules.{entry.Name}.Application",
                $"FinTrack.Modules.{entry.Name}.Infrastructure",
            })
            .Append("Microsoft.EntityFrameworkCore")
            .Append("System.Data")
            .ToArray();

        foreach (var assembly in new[] { reportingApplication, reportingEndpoints })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            AssertSuccess(
                result,
                "Reporting başka modülün DbContext/entity/tablosuna doğrudan erişemez");
        }

        Assert.Contains(
            reportingApplication.GetReferencedAssemblies(),
            assembly => assembly.Name == "FinTrack.ModuleContracts");
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
}
