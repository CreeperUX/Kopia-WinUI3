using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KopiaWinUI3.Services;

public sealed class NotificationDialogService : INotificationDialogService
{
    private DispatcherQueue? _dispatcherQueue;
    private FrameworkElement? _rootElement;

    public void Initialize(Window window)
    {
        _dispatcherQueue = window.DispatcherQueue;
        _rootElement = window.Content as FrameworkElement;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        if (_dispatcherQueue is null || _rootElement is null)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            await ShowErrorCoreAsync(title, message);
            return;
        }

        var completion = new TaskCompletionSource();
        _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await ShowErrorCoreAsync(title, message);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        await completion.Task;
    }

    private async Task ShowErrorCoreAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _rootElement!.XamlRoot,
            Title = title,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            }
        };

        await dialog.ShowAsync();
    }
}
