using FinTrack.ModuleContracts;
using FinTrack.Modules.Debts.Application.Common;
using FinTrack.Modules.Debts.Application.Loans;
using FinTrack.Modules.Debts.Application.PersonalDebts;
using FinTrack.Modules.Debts.Application.Reporting;
using FinTrack.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Modules.Debts.Application;

/// <summary>
/// Handler'lar MediatR olmadan DI ile çözülür (ADR-0003). Kayıtlar açıktır;
/// sihirli tarama yoktur, böylece bağımlılıklar kod üzerinden izlenebilir.
/// </summary>
public static class DebtsApplicationRegistration
{
    public static IServiceCollection AddDebtsApplication(this IServiceCollection services)
    {
        services.AddScoped<ILoanDebtQuery, LoanDebtQuery>();

        // Kredi.
        services.AddScoped<ICommandHandler<CreateLoanCommand, Result<LoanDto>>, CreateLoanHandler>();
        services.AddScoped<ICommandHandler<CancelLoanCommand, Result>, CancelLoanHandler>();
        services.AddScoped<ICommandHandler<PayInstallmentCommand, Result<LoanDto>>, PayInstallmentHandler>();
        services.AddScoped<ICommandHandler<CancelInstallmentPaymentCommand, Result>, CancelInstallmentPaymentHandler>();
        services.AddScoped<IQueryHandler<ListLoansQuery, Result<PagedResult<LoanDto>>>, ListLoansHandler>();
        services.AddScoped<IQueryHandler<GetLoanQuery, Result<LoanDto>>, GetLoanHandler>();

        // Kişisel borç.
        services.AddScoped<ICommandHandler<CreatePersonalDebtCommand, Result<PersonalDebtDto>>, CreatePersonalDebtHandler>();
        services.AddScoped<ICommandHandler<CancelPersonalDebtCommand, Result>, CancelPersonalDebtHandler>();
        services.AddScoped<ICommandHandler<AddRepaymentCommand, Result<RepaymentDto>>, AddRepaymentHandler>();
        services.AddScoped<ICommandHandler<CancelRepaymentCommand, Result>, CancelRepaymentHandler>();
        services.AddScoped<IQueryHandler<ListPersonalDebtsQuery, Result<PagedResult<PersonalDebtDto>>>, ListPersonalDebtsHandler>();
        services.AddScoped<IQueryHandler<GetPersonalDebtQuery, Result<PersonalDebtDto>>, GetPersonalDebtHandler>();
        services.AddScoped<IQueryHandler<ListRepaymentsQuery, Result<IReadOnlyList<RepaymentDto>>>, ListRepaymentsHandler>();

        return services;
    }
}
