using Microsoft.Maui;
using Microsoft.UI.Xaml;

namespace FinTrack.Mobile.WinUI;

/// <summary>
/// Windows giriş noktası; MAUI uygulamasını MauiProgram üzerinden kurar.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
