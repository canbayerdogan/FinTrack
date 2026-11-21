using FinTrack.Mobile.Core.ViewModels;

namespace FinTrack.Mobile.Views;

public partial class AddCardPage : ContentPage
{
    public AddCardPage(AddCardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
