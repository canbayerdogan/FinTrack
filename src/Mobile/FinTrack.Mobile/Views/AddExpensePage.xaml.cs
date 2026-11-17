using FinTrack.Mobile.Core.ViewModels;

namespace FinTrack.Mobile.Views;

public partial class AddExpensePage : ContentPage
{
    private readonly AddExpenseViewModel _viewModel;

    public AddExpensePage(AddExpenseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
