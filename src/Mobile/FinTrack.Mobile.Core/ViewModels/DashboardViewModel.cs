using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Formatting;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;
    private readonly IAppNavigator _navigator;

    public DashboardViewModel(FinTrackApiClient api, IAppNavigator navigator)
    {
        _api = api;
        _navigator = navigator;
        Title = "Ana özet";
        var today = DateOnly.FromDateTime(DateTime.Today);
        Year = today.Year;
        Month = today.Month;
    }

    public ObservableCollection<CategoryAmountItem> Categories { get; } = [];

    public ObservableCollection<UpcomingPaymentItem> UpcomingPayments { get; } = [];

    [ObservableProperty] private int year;

    [ObservableProperty] private int month;

    [ObservableProperty] private string periodLabel = string.Empty;

    [ObservableProperty] private string earnedIncome = MoneyFormat.Format(0m);

    [ObservableProperty] private string otherInflows = MoneyFormat.Format(0m);

    [ObservableProperty] private string expenseNet = MoneyFormat.Format(0m);

    [ObservableProperty] private string difference = MoneyFormat.Format(0m);

    [ObservableProperty] private string totalDebt = MoneyFormat.Format(0m);

    [ObservableProperty] private string disclaimer = string.Empty;

    [ObservableProperty] private int upcomingCount;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            Year = today.Year;
            Month = today.Month;

            var monthlyTask = _api.GetMonthlyReportAsync(Year, Month, cancellationToken);
            var debtTask = _api.GetDebtSummaryAsync(cancellationToken);
            var upcomingTask = _api.GetUpcomingPaymentsAsync(30, cancellationToken);
            await Task.WhenAll(monthlyTask, debtTask, upcomingTask);

            var monthly = await monthlyTask;
            var debt = await debtTask;
            var upcoming = await upcomingTask;

            PeriodLabel = $"{monthly.Period.Year}-{monthly.Period.Month:00} · {monthly.Period.TimeZone}";
            EarnedIncome = MoneyFormat.Format(monthly.EarnedIncome, monthly.Currency);
            OtherInflows = MoneyFormat.Format(
                monthly.OtherInflows.Total + monthly.UnknownInflows.Total,
                monthly.Currency);
            ExpenseNet = MoneyFormat.Format(monthly.Expense.Net, monthly.Currency);
            Difference = MoneyFormat.Format(monthly.IncomeExpenseDifference, monthly.Currency);
            TotalDebt = MoneyFormat.Format(debt.TotalDebt, debt.Currency);
            Disclaimer = monthly.Disclaimer;
            UpcomingCount = upcoming.Items.Count;

            Categories.Clear();
            foreach (var category in monthly.Categories.OrderByDescending(item => item.NetAmount).Take(5))
            {
                Categories.Add(new CategoryAmountItem(
                    category.CategoryName,
                    MoneyFormat.Format(category.NetAmount, monthly.Currency)));
            }

            UpcomingPayments.Clear();
            foreach (var item in upcoming.Items.Take(5))
            {
                UpcomingPayments.Add(ToDisplay(item, upcoming.Currency));
            }
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Raporlar yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenExpensesAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("//expenses", cancellationToken);

    [RelayCommand]
    private async Task OpenCardsAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("//cards", cancellationToken);

    [RelayCommand]
    private async Task OpenReportsAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("//reports", cancellationToken);

    [RelayCommand]
    private async Task OpenDebtsAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("//debts", cancellationToken);

    private static UpcomingPaymentItem ToDisplay(UpcomingPayment item, string currency)
    {
        var detail = item.Type == "LoanInstallment"
            ? $"{item.LoanId:D} · taksit {item.InstallmentNumber}"
            : $"Ekstre {item.StatementId:D}";

        return new UpcomingPaymentItem(
            item.Type,
            item.DueDate.ToString("dd.MM.yyyy"),
            MoneyFormat.Format(item.Amount, currency),
            detail);
    }
}
