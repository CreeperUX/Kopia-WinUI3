using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RcloneWinUI3.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    private Window? _window;

    public void Initialize(Window window)
    {
        _window = window;
    }

    public async Task<string?> PickFolderAsync()
    {
        if (_window is null)
        {
            throw new InvalidOperationException("窗口尚未初始化，无法打开文件夹选择器。");
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop
        };
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
