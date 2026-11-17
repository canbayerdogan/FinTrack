using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Formatting;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class ExpensesViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;
    private readonly IAppNavigator _navigator;

    public ExpensesViewModel(FinTrackApiClient api, IAppNavigator navigator)
    {
        _api = api;
        _navigator = navigator;
        Title = "Harcamalar";
    }

    public ObservableCollection<ExpenseListItem> Items { get; } = [];

    [ObservableProperty] private string summary = "Henüz harcama yok.";

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var result = await _api.GetExpensesAsync(
                new DateOnly(today.Year, today.Month, 1),
                today,
                page: 1,
                pageSize: 100,
                ct: cancellationToken);

            Items.Clear();
            foreach (var expense in result.Items)
            {
                Items.Add(new ExpenseListItem(
                    expense.Id.ToString("D"),
                    expense.OccurredOn.ToString("dd.MM.yyyy"),
                    expense.CategoryName,
                    expense.Description ?? string.Empty,
                    MoneyFormat.Format(expense.NetAmount, expense.Currency),
                    expense.Status));
            }

            Summary = $"{result.TotalCount} kayıt · toplam {result.TotalPages} sayfa";
            ErrorMessage = null;
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Harcamalar yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddExpenseAsync(CancellationToken cancellationToken) =>
        await _navigator.GoToAsync("addexpense", cancellationToken);

    [RelayCommand]
    private async Task GoBackAsync(CancellationToken cancellationToken) =>
        await _navigator.GoBackAsync(cancellationToken);
}
