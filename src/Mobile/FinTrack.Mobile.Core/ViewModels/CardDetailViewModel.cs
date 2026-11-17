using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Abstractions;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Formatting;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class CardDetailViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;

    public CardDetailViewModel(FinTrackApiClient api)
    {
        _api = api;
        Title = "Kart detayı";
    }

    public Guid CardId { get; private set; }

    public ObservableCollection<StatementListItem> Statements { get; } = [];

    public ObservableCollection<ExpenseListItem> Expenses { get; } = [];

    [ObservableProperty] private string cardTitle = string.Empty;

    [ObservableProperty] private string debtText = MoneyFormat.Format(0m);

    [ObservableProperty] private string breakdownText = string.Empty;

    public void Initialize(Guid cardId) => CardId = cardId;

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (CardId == Guid.Empty)
        {
            ErrorMessage = "Kart seçilmedi.";
            return;
        }

        SetBusy(true);
        try
        {
            var balanceTask = _api.GetCardBalanceAsync(CardId, ct: cancellationToken);
            var statementsTask = _api.GetStatementsAsync(CardId, cancellationToken);
            var expensesTask = _api.GetExpensesAsync(cardId: CardId, pageSize: 50, ct: cancellationToken);
            await Task.WhenAll(balanceTask, statementsTask, expensesTask);

            var balance = await balanceTask;
            var statements = await statementsTask;
            var expenses = await expensesTask;

            CardTitle = $"Kart · {balance.CardId:D}";
            DebtText = MoneyFormat.Format(balance.Debt, balance.Currency);
            BreakdownText =
                $"Harcama {MoneyFormat.Format(balance.Breakdown.Expenses, balance.Currency)} · " +
                $"İade {MoneyFormat.Format(balance.Breakdown.Refunds, balance.Currency)} · " +
                $"Avans {MoneyFormat.Format(balance.Breakdown.CashAdvances, balance.Currency)} · " +
                $"Ödeme {MoneyFormat.Format(balance.Breakdown.Payments, balance.Currency)}";

            Statements.Clear();
            foreach (var statement in statements.Items)
            {
                Statements.Add(new StatementListItem(
                    statement.Id.ToString("D"),
                    $"{statement.PeriodStart:dd.MM.yyyy} - {statement.PeriodEnd:dd.MM.yyyy}",
                    statement.DueDate.ToString("dd.MM.yyyy"),
                    MoneyFormat.Format(statement.ComputedDebtAtPeriodEnd, statement.Currency),
                    statement.Status));
            }

            Expenses.Clear();
            foreach (var expense in expenses.Items)
            {
                Expenses.Add(new ExpenseListItem(
                    expense.Id.ToString("D"),
                    expense.OccurredOn.ToString("dd.MM.yyyy"),
                    expense.CategoryName,
                    expense.Description ?? string.Empty,
                    MoneyFormat.Format(expense.NetAmount, expense.Currency),
                    expense.Status));
            }
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Kart detayı yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
