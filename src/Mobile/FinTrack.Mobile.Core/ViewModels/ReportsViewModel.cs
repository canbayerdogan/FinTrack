using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Formatting;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;

    public ReportsViewModel(FinTrackApiClient api)
    {
        _api = api;
        Title = "Raporlar";
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

    [ObservableProperty] private string expenseNet = MoneyFormat.Format(0m);

    [ObservableProperty] private string difference = MoneyFormat.Format(0m);

    [ObservableProperty] private string totalDebt = MoneyFormat.Format(0m);

    [ObservableProperty] private string disclaimer = string.Empty;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Year is < 2000 or > 2100 || Month is < 1 or > 12)
        {
            ErrorMessage = "Geçerli bir yıl (2000-2100) ve ay (1-12) giriniz.";
            return;
        }

        SetBusy(true);
        try
        {
            var monthlyTask = _api.GetMonthlyReportAsync(Year, Month, cancellationToken);
            var debtTask = _api.GetDebtSummaryAsync(cancellationToken);
            var upcomingTask = _api.GetUpcomingPaymentsAsync(30, cancellationToken);
            await Task.WhenAll(monthlyTask, debtTask, upcomingTask);

            var monthly = await monthlyTask;
            var debt = await debtTask;
            var upcoming = await upcomingTask;

            PeriodLabel = $"{monthly.Period.From:dd.MM.yyyy} - {monthly.Period.To:dd.MM.yyyy} · {monthly.Period.TimeZone}";
            EarnedIncome = MoneyFormat.Format(monthly.EarnedIncome, monthly.Currency);
            ExpenseNet = MoneyFormat.Format(monthly.Expense.Net, monthly.Currency);
            Difference = MoneyFormat.Format(monthly.IncomeExpenseDifference, monthly.Currency);
            TotalDebt = MoneyFormat.Format(debt.TotalDebt, debt.Currency);
            Disclaimer = monthly.Disclaimer;

            Categories.Clear();
            foreach (var category in monthly.Categories)
            {
                Categories.Add(new CategoryAmountItem(
                    category.CategoryName,
                    MoneyFormat.Format(category.NetAmount, monthly.Currency)));
            }

            UpcomingPayments.Clear();
            foreach (var item in upcoming.Items)
            {
                var detail = item.Type == "LoanInstallment"
                    ? $"Taksit {item.InstallmentNumber}"
                    : "Kart ekstresi";
                UpcomingPayments.Add(new UpcomingPaymentItem(
                    item.Type,
                    item.DueDate.ToString("dd.MM.yyyy"),
                    MoneyFormat.Format(item.Amount, upcoming.Currency),
                    detail));
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
}
