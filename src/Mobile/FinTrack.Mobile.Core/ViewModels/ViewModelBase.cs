using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FinTrack.Mobile.Core.ViewModels;

/// <summary>
/// Tüm ekran modellerinin ortak durumu. Komutlar çalışırken çift tıklamayı
/// engeller; hata mesajı kullanıcıya gösterilir.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _errorMessage;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !_isBusy;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string Title { get; protected set; } = string.Empty;

    protected bool CanExecute => !IsBusy;

    protected void SetBusy(bool busy)
    {
        IsBusy = busy;
        ErrorMessage = null;
    }

    protected void SetError(Exception exception)
    {
        ErrorMessage = exception is Api.ApiException api
            ? api.UserMessage
            : "İşlem tamamlanamadı. Lütfen tekrar deneyin.";
    }

    protected static bool IsUnauthorized(Api.ApiException exception) => exception.StatusCode == 401;
}
