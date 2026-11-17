using FinTrack.Mobile.Views;

namespace FinTrack.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("addexpense", typeof(AddExpensePage));
        Routing.RegisterRoute("carddetail", typeof(CardDetailPage));
    }
}
