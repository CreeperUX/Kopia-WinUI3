using Microsoft.UI.Xaml;

namespace RcloneWinUI3.Services;

public interface IFolderPickerService
{
    void Initialize(Window window);

    Task<string?> PickFolderAsync();
}
