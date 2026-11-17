using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Mobile.Core.Api;
using FinTrack.Mobile.Core.Formatting;

namespace FinTrack.Mobile.Core.ViewModels;

public partial class DebtsViewModel : ViewModelBase
{
    private readonly FinTrackApiClient _api;

    public DebtsViewModel(FinTrackApiClient api)
    {
        _api = api;
        Title = "Borçlar";
    }

    public ObservableCollection<LoanListItem> Loans { get; } = [];

    public ObservableCollection<DebtorListItem> PersonalDebts { get; } = [];

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        SetBusy(true);
        try
        {
            var loansTask = _api.GetLoansAsync(cancellationToken);
            var debtsTask = _api.GetPersonalDebtsAsync(cancellationToken);
            await Task.WhenAll(loansTask, debtsTask);

            Loans.Clear();
            foreach (var loan in (await loansTask).Items)
            {
                Loans.Add(new LoanListItem(
                    loan.Id.ToString("D"),
                    string.IsNullOrWhiteSpace(loan.BankName) ? "Kredi" : loan.BankName,
                    MoneyFormat.Format(loan.RemainingAmount, loan.Currency),
                    loan.Status));
            }

            PersonalDebts.Clear();
            foreach (var debt in (await debtsTask).Items)
            {
                PersonalDebts.Add(new DebtorListItem(
                    debt.Id.ToString("D"),
                    debt.CounterpartyName,
                    MoneyFormat.Format(debt.RemainingAmount, debt.Currency),
                    debt.Status));
            }
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Borçlar yüklenemedi. Bağlantınızı kontrol ediniz.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
