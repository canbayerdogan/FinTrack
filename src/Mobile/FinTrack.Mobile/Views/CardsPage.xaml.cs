using FinTrack.Mobile.Core.ViewModels;

namespace FinTrack.Mobile.Views;

public partial class CardsPage : ContentPage
{
    private readonly CardsViewModel _viewModel;

    public CardsPage(CardsViewModel viewModel)
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

    private async void OnCardSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CardListItem card)
        {
            await _viewModel.OpenCardCommand.ExecuteAsync(card);
            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
