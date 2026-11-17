using FinTrack.Mobile.Core.ViewModels;
using Microsoft.Maui.Controls;

namespace FinTrack.Mobile.Views;

public partial class CardDetailPage : ContentPage, IQueryAttributable
{
    private readonly CardDetailViewModel _viewModel;

    public CardDetailPage(CardDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> queryAttributes)
    {
        if (queryAttributes.TryGetValue("id", out var value) &&
            Guid.TryParse(value?.ToString(), out var cardId))
        {
            _viewModel.Initialize(cardId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
