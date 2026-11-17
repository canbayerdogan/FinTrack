using FinTrack.Mobile.Core.ViewModels;

namespace FinTrack.Mobile.Views;

public partial class DebtsPage : ContentPage
{
    private readonly DebtsViewModel _viewModel;

    public DebtsPage(DebtsViewModel viewModel)
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
