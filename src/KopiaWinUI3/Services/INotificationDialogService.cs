using Microsoft.UI.Xaml;

namespace KopiaWinUI3.Services;

public interface INotificationDialogService
{
    void Initialize(Window window);

    Task ShowErrorAsync(string title, string message);
}
